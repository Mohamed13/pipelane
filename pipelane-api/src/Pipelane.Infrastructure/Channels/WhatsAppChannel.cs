using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Pipelane.Application.Abstractions;
using Pipelane.Application.Storage;
using Pipelane.Domain.Entities;
using Pipelane.Domain.Enums;

namespace Pipelane.Infrastructure.Channels;

public sealed class WhatsAppChannel : IMessageChannel
{
    private const string ClientName = "WhatsAppCloud";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions StrictJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = false
    };

    private readonly IHttpClientFactory _httpFactory;
    private readonly IChannelConfigurationProvider _configProvider;
    private readonly IAppDbContext _db;
    private readonly ILogger<WhatsAppChannel> _logger;
    private readonly TimeProvider _clock;

    public WhatsAppChannel(
        IHttpClientFactory httpFactory,
        IChannelConfigurationProvider configProvider,
        IAppDbContext db,
        ILogger<WhatsAppChannel> logger,
        TimeProvider? clock = null)
    {
        _httpFactory = httpFactory;
        _configProvider = configProvider;
        _db = db;
        _logger = logger;
        _clock = clock ?? TimeProvider.System;
    }

    public Channel Channel => Channel.Whatsapp;

    public async Task<SendResult> SendTemplateAsync(Contact contact, Template template, IDictionary<string, string> variables, SendMeta meta, CancellationToken ct)
    {
        var config = await _configProvider.GetWhatsAppConfigAsync(contact.TenantId, ct).ConfigureAwait(false);
        if (config is null)
        {
            return new SendResult(false, null, "WhatsApp Cloud not configured for tenant.");
        }

        if (string.IsNullOrWhiteSpace(contact.Phone))
        {
            return new SendResult(false, null, "Contact phone missing.");
        }

        if (string.IsNullOrWhiteSpace(template.CoreSchemaJson))
        {
            _logger.LogWarning("Template {TemplateId} missing CoreSchemaJson for WhatsApp send.", template.Id);
            return new SendResult(false, null, "Template missing WhatsApp schema.");
        }

        WhatsAppTemplateDefinition? schema;
        try
        {
            schema = JsonSerializer.Deserialize<WhatsAppTemplateDefinition>(template.CoreSchemaJson, StrictJsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid WhatsApp template schema for template {TemplateId}", template.Id);
            return new SendResult(false, null, "Template schema invalid.");
        }

        if (schema is null || string.IsNullOrWhiteSpace(schema.Name))
        {
            return new SendResult(false, null, "Template schema incomplete.");
        }

        var payload = new WhatsAppMessageRequest
        {
            To = NormalizePhone(contact.Phone),
            Type = "template",
            Template = BuildTemplatePayload(schema, template, variables)
        };

        if (payload.Template is null)
        {
            return new SendResult(false, null, "Template components invalid.");
        }

        return await SendAsync(config, payload, contact, ct).ConfigureAwait(false);
    }

    public async Task<SendResult> SendTextAsync(Contact contact, string text, SendMeta meta, CancellationToken ct)
    {
        var config = await _configProvider.GetWhatsAppConfigAsync(contact.TenantId, ct).ConfigureAwait(false);
        if (config is null)
        {
            return new SendResult(false, null, "WhatsApp Cloud not configured for tenant.");
        }

        if (string.IsNullOrWhiteSpace(contact.Phone))
        {
            return new SendResult(false, null, "Contact phone missing.");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return new SendResult(false, null, "Message text missing.");
        }

        var payload = new WhatsAppMessageRequest
        {
            To = NormalizePhone(contact.Phone),
            Type = "text",
            Text = new WhatsAppTextContent
            {
                PreviewUrl = false,
                Body = text
            }
        };

        return await SendAsync(config, payload, contact, ct).ConfigureAwait(false);
    }

    public async Task<WebhookResult> HandleWebhookAsync(string body, IDictionary<string, string> headers, CancellationToken ct)
    {
        if (!headers.TryGetValue("x-tenant-id", out var tenantValue) || !Guid.TryParse(tenantValue, out var tenantId))
        {
            _logger.LogWarning("WhatsApp webhook missing tenant header.");
            return new WebhookResult(false, "missing_tenant");
        }

        var config = await _configProvider.GetWhatsAppConfigAsync(tenantId, ct).ConfigureAwait(false);
        if (config is null)
        {
            _logger.LogWarning("WhatsApp webhook received for tenant {TenantId} without configuration.", tenantId);
            return new WebhookResult(false, "config_missing");
        }

        if (!headers.TryGetValue("X-Hub-Signature-256", out var signature) || !VerifySignature(signature, body, config.AppSecret))
        {
            _logger.LogWarning("WhatsApp webhook signature invalid for tenant {TenantId}", tenantId);
            return new WebhookResult(false, "invalid_signature");
        }

        WhatsAppWebhookEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<WhatsAppWebhookEnvelope>(body, StrictJsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "WhatsApp webhook payload invalid for tenant {TenantId}", tenantId);
            return new WebhookResult(false, "invalid_payload");
        }

        if (envelope?.Entry is null)
        {
            return new WebhookResult(true, null);
        }

        foreach (var entry in envelope.Entry)
        {
            if (entry.Changes is null)
            {
                continue;
            }

            foreach (var change in entry.Changes)
            {
                if (change.Value is null)
                {
                    continue;
                }

                await HandleStatusesAsync(change.Value.Statuses, tenantId, body, ct).ConfigureAwait(false);
                await HandleInboundMessagesAsync(change.Value.Messages, change.Value.Metadata, tenantId, body, ct).ConfigureAwait(false);
            }
        }

        return new WebhookResult(true, null);
    }

    public Task<bool> ValidateTemplateAsync(Template t, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(t.CoreSchemaJson))
        {
            return Task.FromResult(false);
        }

        try
        {
            var definition = JsonSerializer.Deserialize<WhatsAppTemplateDefinition>(t.CoreSchemaJson, StrictJsonOptions);
            return Task.FromResult(definition is not null && !string.IsNullOrWhiteSpace(definition.Name));
        }
        catch (JsonException)
        {
            return Task.FromResult(false);
        }
    }

    private async Task HandleStatusesAsync(IReadOnlyList<WhatsAppStatus>? statuses, Guid tenantId, string raw, CancellationToken ct)
    {
        if (statuses is null || statuses.Count == 0)
        {
            return;
        }

        foreach (var status in statuses)
        {
            if (string.IsNullOrWhiteSpace(status.Id) || string.IsNullOrWhiteSpace(status.Status))
            {
                continue;
            }

            var providerEventId = status.ExtractEventId();

            var exists = await _db.MessageEvents
                .AsNoTracking()
                .AnyAsync(e => e.Provider == "whatsapp" && e.ProviderEventId == providerEventId, ct)
                .ConfigureAwait(false);

            if (exists)
            {
                continue;
            }

            var message = await _db.Messages
                .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Provider == "whatsapp" && m.ProviderMessageId == status.Id, ct)
                .ConfigureAwait(false);

            if (message is null)
            {
                _logger.LogWarning("WhatsApp status for message {ProviderMessageId} not found (tenant {TenantId})", status.Id, tenantId);
                continue;
            }

            var now = _clock.GetUtcNow().UtcDateTime;
            var mappedStatus = MapStatus(status.Status);
            if (mappedStatus is MessageStatus.Delivered or MessageStatus.Opened)
            {
                if (mappedStatus == MessageStatus.Delivered)
                {
                    message.DeliveredAt = now;
                }
                else if (mappedStatus == MessageStatus.Opened)
                {
                    message.OpenedAt = now;
                }
            }

            if (mappedStatus == MessageStatus.Failed && status.Errors is { Count: > 0 })
            {
                var firstError = status.Errors[0];
                message.ErrorCode = firstError?.Code?.ToString();
                message.ErrorReason = firstError?.Message ?? firstError?.Title;
                message.FailedAt = now;
            }

            message.Status = mappedStatus;
            _db.MessageEvents.Add(new MessageEvent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                MessageId = message.Id,
                Type = MapEventType(mappedStatus),
                Provider = "whatsapp",
                ProviderEventId = providerEventId,
                Raw = raw,
                CreatedAt = now
            });

            await _db.SaveChangesAsync(ct).ConfigureAwait(false);

            _logger.LogInformation("WhatsApp status processed tenant={TenantId} providerMessageId={ProviderMessageId} status={Status} eventId={EventId}",
                tenantId,
                status.Id,
                mappedStatus,
                providerEventId);
        }
    }

    private async Task HandleInboundMessagesAsync(IReadOnlyList<WhatsAppInboundMessage>? messages, WhatsAppMetadata? metadata, Guid tenantId, string raw, CancellationToken ct)
    {
        if (messages is null || messages.Count == 0)
        {
            return;
        }

        foreach (var message in messages)
        {
            if (string.IsNullOrWhiteSpace(message.Id) || string.IsNullOrWhiteSpace(message.From))
            {
                continue;
            }

            var providerEventId = $"{message.Id}:in";
            var exists = await _db.MessageEvents
                .AsNoTracking()
                .AnyAsync(e => e.Provider == "whatsapp" && e.ProviderEventId == providerEventId, ct)
                .ConfigureAwait(false);

            if (exists)
            {
                continue;
            }

            var contact = await _db.Contacts
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Phone == NormalizePhone(message.From ?? string.Empty), ct)
                .ConfigureAwait(false);

            if (contact is null)
            {
                _logger.LogInformation("Inbound WhatsApp message skipped, contact not found for phone {Phone} tenant {TenantId}", message.From, tenantId);
                continue;
            }

            var conversation = await _db.Conversations
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.ContactId == contact.Id, ct)
                .ConfigureAwait(false);

            if (conversation is null)
            {
                conversation = new Conversation
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ContactId = contact.Id,
                    PrimaryChannel = Channel.Whatsapp,
                    ProviderThreadId = metadata?.PhoneNumberId,
                    CreatedAt = _clock.GetUtcNow().UtcDateTime
                };
                _db.Conversations.Add(conversation);
            }

            var textBody = message.Text?.Body ?? message.Interactive?.ButtonReply?.Title ?? message.Interactive?.ListReply?.Title;

            var messageEntity = new Message
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ConversationId = conversation.Id,
                Channel = Channel.Whatsapp,
                Direction = MessageDirection.In,
                Type = message.Type switch
                {
                    "text" => MessageType.Text,
                    "interactive" => MessageType.Text,
                    _ => MessageType.Text
                },
                PayloadJson = JsonSerializer.Serialize(message, JsonOptions),
                Status = MessageStatus.Delivered,
                Provider = "whatsapp",
                ProviderMessageId = message.Id,
                CreatedAt = _clock.GetUtcNow().UtcDateTime
            };

            _db.Messages.Add(messageEntity);
            _db.MessageEvents.Add(new MessageEvent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                MessageId = messageEntity.Id,
                Type = MessageEventType.Delivered,
                Provider = "whatsapp",
                ProviderEventId = providerEventId,
                Raw = raw,
                CreatedAt = _clock.GetUtcNow().UtcDateTime
            });

            await _db.SaveChangesAsync(ct).ConfigureAwait(false);

            _logger.LogInformation("Inbound WhatsApp message stored tenant={TenantId} providerMessageId={ProviderMessageId}", tenantId, message.Id);
        }
    }

    private async Task<SendResult> SendAsync(WhatsAppChannelConfig config, WhatsAppMessageRequest request, Contact contact, CancellationToken ct)
    {
        var client = _httpFactory.CreateClient(ClientName);
        var endpoint = $"{config.PhoneNumberId}/messages";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(request, JsonOptions), Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.AccessToken);

        try
        {
            using var response = await client.SendAsync(httpRequest, ct).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("WhatsApp send failed tenant={TenantId} contact={ContactId} status={Status} body={Body}", contact.TenantId, contact.Id, response.StatusCode, body);
                return new SendResult(false, null, body);
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<WhatsAppSendResponse>(body, StrictJsonOptions);
                var providerMessageId = parsed?.Messages?.FirstOrDefault()?.Id;
                _logger.LogInformation("WhatsApp message sent tenant={TenantId} contact={ContactId} providerMessageId={ProviderMessageId}", contact.TenantId, contact.Id, providerMessageId);
                return new SendResult(true, providerMessageId, null);
            }
            catch (JsonException)
            {
                _logger.LogWarning("WhatsApp send succeeded but response parse failed tenant={TenantId} body={Body}", contact.TenantId, body);
                return new SendResult(true, null, null);
            }
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WhatsApp send error tenant={TenantId} contact={ContactId}", contact.TenantId, contact.Id);
            return new SendResult(false, null, ex.Message);
        }
    }

    private static WhatsAppTemplateContent? BuildTemplatePayload(WhatsAppTemplateDefinition schema, Template template, IDictionary<string, string> variables)
    {
        var languageCode = schema.Language ?? template.Lang ?? "en_US";
        if (schema.Components is null || schema.Components.Count == 0)
        {
            return null;
        }

        var components = new List<WhatsAppTemplateComponentPayload>();
        foreach (var component in schema.Components)
        {
            if (string.IsNullOrWhiteSpace(component.Type))
            {
                continue;
            }

            var parameters = new List<WhatsAppTemplateParameterPayload>();
            if (component.Parameters is not null)
            {
                foreach (var parameter in component.Parameters)
                {
                    if (!string.IsNullOrWhiteSpace(parameter.Key) && variables.TryGetValue(parameter.Key, out var value))
                    {
                        parameters.Add(new WhatsAppTemplateParameterPayload
                        {
                            Type = parameter.Type ?? "text",
                            Text = value
                        });
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(parameter.Text))
                    {
                        parameters.Add(new WhatsAppTemplateParameterPayload
                        {
                            Type = parameter.Type ?? "text",
                            Text = parameter.Text
                        });
                    }
                }
            }

            components.Add(new WhatsAppTemplateComponentPayload
            {
                Type = component.Type.ToLowerInvariant(),
                SubType = component.SubType,
                Parameters = parameters
            });
        }

        if (components.Count == 0)
        {
            return null;
        }

        return new WhatsAppTemplateContent
        {
            Name = schema.Name!,
            Language = new WhatsAppLanguage { Code = languageCode },
            Components = components
        };
    }

    private static string NormalizePhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return string.Empty;
        }

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (string.IsNullOrEmpty(digits))
        {
            return string.Empty;
        }

        if (digits.StartsWith("00", StringComparison.Ordinal))
        {
            digits = digits[2..];
        }
        if (!digits.StartsWith("+", StringComparison.Ordinal))
        {
            digits = "+" + digits;
        }
        return digits;
    }

    private static bool VerifySignature(string providedSignature, string payload, string secret)
    {
        if (string.IsNullOrWhiteSpace(providedSignature))
        {
            return false;
        }

        var expectedPrefix = "sha256=";
        var signature = providedSignature.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase)
            ? providedSignature[expectedPrefix.Length..]
            : providedSignature;

        var bodyBytes = Encoding.UTF8.GetBytes(payload);
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hashBytes = hmac.ComputeHash(bodyBytes);
        var hash = Convert.ToHexString(hashBytes).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(hash), Encoding.UTF8.GetBytes(signature.ToLowerInvariant()));
    }

    private static MessageStatus MapStatus(string status)
    {
        return status switch
        {
            "sent" => MessageStatus.Sent,
            "delivered" => MessageStatus.Delivered,
            "read" => MessageStatus.Opened,
            "failed" or "undelivered" => MessageStatus.Failed,
            _ => MessageStatus.Sent
        };
    }

    private static MessageEventType MapEventType(MessageStatus status)
    {
        return status switch
        {
            MessageStatus.Delivered => MessageEventType.Delivered,
            MessageStatus.Opened => MessageEventType.Opened,
            MessageStatus.Failed => MessageEventType.Failed,
            MessageStatus.Bounced => MessageEventType.Bounced,
            _ => MessageEventType.Sent
        };
    }

    private sealed record WhatsAppSendResponse(
        [property: JsonPropertyName("messages")] IReadOnlyList<WhatsAppSendResponseEntry>? Messages);

    private sealed record WhatsAppSendResponseEntry(
        [property: JsonPropertyName("id")] string Id);

    private sealed record WhatsAppTemplateDefinition(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("language")] string? Language,
        [property: JsonPropertyName("components")] IReadOnlyList<WhatsAppTemplateDefinitionComponent>? Components);

    private sealed record WhatsAppTemplateDefinitionComponent(
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("sub_type")] string? SubType,
        [property: JsonPropertyName("parameters")] IReadOnlyList<WhatsAppTemplateDefinitionParameter>? Parameters);

    private sealed record WhatsAppTemplateDefinitionParameter(
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("text")] string? Text,
        [property: JsonPropertyName("key")] string? Key);

    private sealed class WhatsAppMessageRequest
    {
        [JsonPropertyName("messaging_product")]
        public string MessagingProduct { get; set; } = "whatsapp";

        [JsonPropertyName("to")]
        public string To { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = "text";

        [JsonPropertyName("template")]
        public WhatsAppTemplateContent? Template { get; set; }

        [JsonPropertyName("text")]
        public WhatsAppTextContent? Text { get; set; }
    }

    private sealed class WhatsAppTextContent
    {
        [JsonPropertyName("preview_url")]
        public bool PreviewUrl { get; set; }

        [JsonPropertyName("body")]
        public string Body { get; set; } = string.Empty;
    }

    private sealed class WhatsAppTemplateContent
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("language")]
        public WhatsAppLanguage Language { get; set; } = new();

        [JsonPropertyName("components")]
        public IReadOnlyList<WhatsAppTemplateComponentPayload> Components { get; set; } = Array.Empty<WhatsAppTemplateComponentPayload>();
    }

    private sealed class WhatsAppLanguage
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = "en_US";
    }

    private sealed class WhatsAppTemplateComponentPayload
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "body";

        [JsonPropertyName("sub_type")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? SubType { get; set; }

        [JsonPropertyName("parameters")]
        public IReadOnlyList<WhatsAppTemplateParameterPayload> Parameters { get; set; } = Array.Empty<WhatsAppTemplateParameterPayload>();
    }

    private sealed class WhatsAppTemplateParameterPayload
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "text";

        [JsonPropertyName("text")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Text { get; set; }
    }

    private sealed record WhatsAppWebhookEnvelope(
        [property: JsonPropertyName("entry")] IReadOnlyList<WhatsAppEntry>? Entry);

    private sealed record WhatsAppEntry(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("changes")] IReadOnlyList<WhatsAppChange>? Changes);

    private sealed record WhatsAppChange(
        [property: JsonPropertyName("value")] WhatsAppChangeValue? Value);

    private sealed record WhatsAppChangeValue(
        [property: JsonPropertyName("metadata")] WhatsAppMetadata? Metadata,
        [property: JsonPropertyName("statuses")] IReadOnlyList<WhatsAppStatus>? Statuses,
        [property: JsonPropertyName("messages")] IReadOnlyList<WhatsAppInboundMessage>? Messages);

    private sealed record WhatsAppMetadata(
        [property: JsonPropertyName("phone_number_id")] string? PhoneNumberId);

    private sealed record WhatsAppStatus(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("timestamp")] string? Timestamp,
        [property: JsonPropertyName("errors")] IReadOnlyList<WhatsAppStatusError?>? Errors)
    {
        public string ExtractEventId()
        {
            if (!string.IsNullOrWhiteSpace(Timestamp))
            {
                return $"{Id}:{Timestamp}";
            }

            return $"{Id}:{Status}";
        }
    }

    private sealed record WhatsAppStatusError(
        [property: JsonPropertyName("code")] int? Code,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("message")] string? Message);

    private sealed record WhatsAppInboundMessage(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("from")] string? From,
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("text")] WhatsAppInboundText? Text,
        [property: JsonPropertyName("interactive")] WhatsAppInboundInteractive? Interactive);

    private sealed record WhatsAppInboundText(
        [property: JsonPropertyName("body")] string? Body);

    private sealed record WhatsAppInboundInteractive(
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("button_reply")] WhatsAppInteractiveButtonReply? ButtonReply,
        [property: JsonPropertyName("list_reply")] WhatsAppInteractiveListReply? ListReply);

    private sealed record WhatsAppInteractiveButtonReply(
        [property: JsonPropertyName("title")] string? Title);

    private sealed record WhatsAppInteractiveListReply(
        [property: JsonPropertyName("title")] string? Title);
}

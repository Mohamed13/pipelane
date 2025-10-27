using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Pipelane.Application.Abstractions;
using Pipelane.Application.Storage;
using Pipelane.Domain.Entities;
using Pipelane.Domain.Enums;

namespace Pipelane.Infrastructure.Channels;

public sealed class SmsChannel : IMessageChannel
{
    private const string ClientName = "Twilio";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Regex TokenRegex = new("{{(.*?)}}", RegexOptions.Compiled);

    private readonly IHttpClientFactory _httpFactory;
    private readonly IChannelConfigurationProvider _configProvider;
    private readonly IAppDbContext _db;
    private readonly ILogger<SmsChannel> _logger;
    private readonly TimeProvider _clock;

    public SmsChannel(
        IHttpClientFactory httpFactory,
        IChannelConfigurationProvider configProvider,
        IAppDbContext db,
        ILogger<SmsChannel> logger,
        TimeProvider? clock = null)
    {
        _httpFactory = httpFactory;
        _configProvider = configProvider;
        _db = db;
        _logger = logger;
        _clock = clock ?? TimeProvider.System;
    }

    public Channel Channel => Channel.Sms;

    public async Task<SendResult> SendTemplateAsync(Contact contact, Template template, IDictionary<string, string> variables, SendMeta meta, CancellationToken ct)
    {
        var body = RenderTemplate(template, variables);
        return await SendMessageAsync(contact, body, ct).ConfigureAwait(false);
    }

    public async Task<SendResult> SendTextAsync(Contact contact, string text, SendMeta meta, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new SendResult(false, null, "SMS text cannot be empty.");
        }

        return await SendMessageAsync(contact, text, ct).ConfigureAwait(false);
    }

    public Task<bool> ValidateTemplateAsync(Template template, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(template.CoreSchemaJson))
        {
            return Task.FromResult(false);
        }

        try
        {
            var schema = JsonSerializer.Deserialize<SmsTemplateSchema>(template.CoreSchemaJson, JsonOptions);
            return Task.FromResult(schema is not null && !string.IsNullOrWhiteSpace(schema.Body));
        }
        catch (JsonException)
        {
            return Task.FromResult(false);
        }
    }

    public async Task<WebhookResult> HandleWebhookAsync(string body, IDictionary<string, string> headers, CancellationToken ct)
    {
        if (!headers.TryGetValue("x-tenant-id", out var tenantValue) || !Guid.TryParse(tenantValue, out var tenantId))
        {
            _logger.LogWarning("Twilio webhook missing tenant header.");
            return new WebhookResult(false, "missing_tenant");
        }

        if (!headers.TryGetValue("x-webhook-kind", out var kind))
        {
            _logger.LogWarning("Twilio webhook missing kind header.");
            return new WebhookResult(false, "missing_kind");
        }

        var config = await _configProvider.GetTwilioConfigAsync(tenantId, ct).ConfigureAwait(false);
        if (config is null)
        {
            _logger.LogWarning("Twilio webhook received for tenant {TenantId} without configuration.", tenantId);
            return new WebhookResult(false, "config_missing");
        }

        if (!headers.TryGetValue("X-Twilio-Signature", out var signature) ||
            !headers.TryGetValue("x-request-url", out var requestUrl))
        {
            _logger.LogWarning("Twilio webhook missing signature metadata tenant={TenantId}", tenantId);
            return new WebhookResult(false, "signature_missing");
        }

        var form = ParseForm(body);
        if (!VerifyTwilioSignature(config.AuthToken, requestUrl, form, signature))
        {
            _logger.LogWarning("Twilio webhook signature invalid tenant={TenantId}", tenantId);
            return new WebhookResult(false, "invalid_signature");
        }

        if (string.Equals(kind, "status", StringComparison.OrdinalIgnoreCase))
        {
            await HandleStatusAsync(tenantId, form, body, ct).ConfigureAwait(false);
            return new WebhookResult(true, null);
        }

        if (string.Equals(kind, "inbound", StringComparison.OrdinalIgnoreCase))
        {
            await HandleInboundAsync(tenantId, form, body, ct).ConfigureAwait(false);
            return new WebhookResult(true, null);
        }

        return new WebhookResult(false, "unknown_kind");
    }

    private async Task HandleStatusAsync(Guid tenantId, Dictionary<string, string> form, string raw, CancellationToken ct)
    {
        if (!form.TryGetValue("MessageSid", out var messageSid) || string.IsNullOrWhiteSpace(messageSid))
        {
            return;
        }

        var status = form.TryGetValue("MessageStatus", out var messageStatus)
            ? messageStatus
            : form.TryGetValue("SmsStatus", out var smsStatus) ? smsStatus : null;

        if (string.IsNullOrWhiteSpace(status))
        {
            return;
        }

        var statusNormalized = status.ToLowerInvariant();
        var providerEventId = $"{messageSid}:{statusNormalized}:{form.GetValueOrDefault("Timestamp") ?? form.GetValueOrDefault("EventType") ?? "status"}";

        var exists = await _db.MessageEvents
            .AsNoTracking()
            .AnyAsync(e => e.Provider == "twilio" && e.ProviderEventId == providerEventId, ct)
            .ConfigureAwait(false);

        if (exists)
        {
            return;
        }

        var message = await _db.Messages
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Provider == "twilio" && m.ProviderMessageId == messageSid, ct)
            .ConfigureAwait(false);

        if (message is null)
        {
            _logger.LogWarning("Twilio status received for unknown message {MessageSid} tenant={TenantId}", messageSid, tenantId);
            return;
        }

        var mappedStatus = MapStatus(statusNormalized);
        var now = _clock.GetUtcNow().UtcDateTime;

        switch (mappedStatus)
        {
            case MessageStatus.Delivered:
                message.DeliveredAt = now;
                break;
            case MessageStatus.Opened:
                message.OpenedAt = now;
                break;
            case MessageStatus.Failed:
                message.FailedAt = now;
                message.ErrorReason = form.GetValueOrDefault("ErrorMessage") ?? form.GetValueOrDefault("ErrorCode");
                message.ErrorCode = form.GetValueOrDefault("ErrorCode");
                break;
        }

        message.Status = mappedStatus;

        _db.MessageEvents.Add(new MessageEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            MessageId = message.Id,
            Type = MapEventType(mappedStatus),
            Provider = "twilio",
            ProviderEventId = providerEventId,
            Raw = raw,
            CreatedAt = now
        });

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("Twilio status processed tenant={TenantId} providerMessageId={ProviderMessageId} status={Status}",
            tenantId,
            messageSid,
            mappedStatus);
    }

    private async Task HandleInboundAsync(Guid tenantId, Dictionary<string, string> form, string raw, CancellationToken ct)
    {
        if (!form.TryGetValue("MessageSid", out var messageSid) || string.IsNullOrWhiteSpace(messageSid))
        {
            return;
        }

        var providerEventId = $"{messageSid}:inbound";
        var exists = await _db.MessageEvents
            .AsNoTracking()
            .AnyAsync(e => e.Provider == "twilio" && e.ProviderEventId == providerEventId, ct)
            .ConfigureAwait(false);

        if (exists)
        {
            return;
        }

        var from = form.GetValueOrDefault("From");
        if (string.IsNullOrWhiteSpace(from))
        {
            return;
        }

        var normalizedFrom = NormalizePhone(from);

        var contact = await _db.Contacts
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Phone == normalizedFrom, ct)
            .ConfigureAwait(false);

        if (contact is null)
        {
            _logger.LogInformation("Inbound Twilio message skipped, contact not found phone={Phone} tenant={TenantId}", normalizedFrom, tenantId);
            return;
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
                PrimaryChannel = Channel.Sms,
                ProviderThreadId = form.GetValueOrDefault("To"),
                CreatedAt = _clock.GetUtcNow().UtcDateTime
            };
            _db.Conversations.Add(conversation);
        }

        var timestamp = _clock.GetUtcNow().UtcDateTime;

        var messageEntity = new Message
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ConversationId = conversation.Id,
            Channel = Channel.Sms,
            Direction = MessageDirection.In,
            Type = MessageType.Text,
            PayloadJson = JsonSerializer.Serialize(form, JsonOptions),
            Status = MessageStatus.Delivered,
            Provider = "twilio",
            ProviderMessageId = messageSid,
            CreatedAt = timestamp
        };

        _db.Messages.Add(messageEntity);
        _db.MessageEvents.Add(new MessageEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            MessageId = messageEntity.Id,
            Type = MessageEventType.Delivered,
            Provider = "twilio",
            ProviderEventId = providerEventId,
            Raw = raw,
            CreatedAt = timestamp
        });

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("Inbound Twilio message stored tenant={TenantId} providerMessageId={ProviderMessageId}", tenantId, messageSid);
    }

    private async Task<SendResult> SendMessageAsync(Contact contact, string body, CancellationToken ct)
    {
        var config = await _configProvider.GetTwilioConfigAsync(contact.TenantId, ct).ConfigureAwait(false);
        if (config is null)
        {
            return new SendResult(false, null, "Twilio not configured for tenant.");
        }

        if (string.IsNullOrWhiteSpace(contact.Phone))
        {
            return new SendResult(false, null, "Contact phone missing.");
        }

        var normalizedPhone = NormalizePhone(contact.Phone);
        if (string.IsNullOrWhiteSpace(normalizedPhone))
        {
            return new SendResult(false, null, "Invalid contact phone number.");
        }

        var content = new List<KeyValuePair<string, string>>
        {
            new("To", normalizedPhone),
            new("Body", body)
        };

        if (!string.IsNullOrWhiteSpace(config.MessagingServiceSid))
        {
            content.Add(new KeyValuePair<string, string>("MessagingServiceSid", config.MessagingServiceSid));
        }
        else if (!string.IsNullOrWhiteSpace(config.FromNumber))
        {
            content.Add(new KeyValuePair<string, string>("From", config.FromNumber));
        }
        else
        {
            return new SendResult(false, null, "Twilio configuration missing MessagingServiceSid or From number.");
        }

        var request = new HttpRequestMessage(HttpMethod.Post, $"Accounts/{config.AccountSid}/Messages.json")
        {
            Content = new FormUrlEncodedContent(content)
        };

        var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{config.AccountSid}:{config.AuthToken}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authToken);

        var client = _httpFactory.CreateClient(ClientName);

        try
        {
            using var response = await client.SendAsync(request, ct).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Twilio send failed tenant={TenantId} contact={ContactId} status={Status} body={Body}",
                    contact.TenantId, contact.Id, response.StatusCode, responseBody);
                return new SendResult(false, null, responseBody);
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<TwilioSendResponse>(responseBody, JsonOptions);
                var sid = parsed?.Sid;
                _logger.LogInformation("Twilio message sent tenant={TenantId} contact={ContactId} sid={Sid}", contact.TenantId, contact.Id, sid);
                return new SendResult(true, sid, null);
            }
            catch (JsonException)
            {
                _logger.LogWarning("Twilio send succeeded but response parse failed tenant={TenantId} body={Body}", contact.TenantId, responseBody);
                return new SendResult(true, null, null);
            }
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Twilio send error tenant={TenantId} contact={ContactId}", contact.TenantId, contact.Id);
            return new SendResult(false, null, ex.Message);
        }
    }

    private static string RenderTemplate(Template template, IDictionary<string, string> variables)
    {
        if (!string.IsNullOrWhiteSpace(template.CoreSchemaJson))
        {
            try
            {
                var schema = JsonSerializer.Deserialize<SmsTemplateSchema>(template.CoreSchemaJson, JsonOptions);
                if (schema is not null && !string.IsNullOrWhiteSpace(schema.Body))
                {
                    return ReplaceTokens(schema.Body, variables);
                }
            }
            catch (JsonException)
            {
            }
        }

        return ReplaceTokens(template.Name, variables);
    }

    private static string ReplaceTokens(string input, IDictionary<string, string> variables)
    {
        return TokenRegex.Replace(input, match =>
        {
            var key = match.Groups[1].Value.Trim();
            return variables.TryGetValue(key, out var value) ? value : string.Empty;
        });
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

    private static Dictionary<string, string> ParseForm(string body)
    {
        var query = QueryHelpers.ParseQuery(string.IsNullOrWhiteSpace(body) ? string.Empty : (body.StartsWith("?") ? body : $"?{body}"));
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in query)
        {
            dict[kvp.Key] = kvp.Value.ToString();
        }
        return dict;
    }

    private static bool VerifyTwilioSignature(string authToken, string requestUrl, IReadOnlyDictionary<string, string> form, string signature)
    {
        if (string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        var data = new StringBuilder(requestUrl);
        foreach (var kvp in form.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            data.Append(kvp.Key).Append(kvp.Value);
        }

        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(authToken));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data.ToString()));
        var expected = Convert.ToBase64String(hash);

        try
        {
            return CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(expected), Convert.FromBase64String(signature));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static MessageStatus MapStatus(string status)
    {
        return status switch
        {
            "queued" or "accepted" or "scheduled" => MessageStatus.Queued,
            "sending" or "sent" => MessageStatus.Sent,
            "delivered" => MessageStatus.Delivered,
            "read" => MessageStatus.Opened,
            "undelivered" or "failed" => MessageStatus.Failed,
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
            MessageStatus.Queued => MessageEventType.Sent,
            _ => MessageEventType.Sent
        };
    }

    private sealed record SmsTemplateSchema([property: JsonPropertyName("body")] string? Body);

    private sealed record TwilioSendResponse([property: JsonPropertyName("sid")] string? Sid);
}

using System.Diagnostics;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Pipelane.Application.Storage;
using Pipelane.Domain.Entities;
using Pipelane.Domain.Enums;
using Pipelane.Application.Common;

namespace Pipelane.Infrastructure.Webhooks;

public sealed class ResendWebhookProcessor
{
    private const string ProviderName = "resend";
    private readonly IAppDbContext _db;
    private readonly ILogger<ResendWebhookProcessor> _logger;

    private static readonly IReadOnlyDictionary<string, (MessageEventType EventType, MessageStatus Status)> EventMap = new Dictionary<string, (MessageEventType, MessageStatus)>(StringComparer.OrdinalIgnoreCase)
    {
        ["email.sent"] = (MessageEventType.Sent, MessageStatus.Sent),
        ["email.delivered"] = (MessageEventType.Delivered, MessageStatus.Delivered),
        ["email.opened"] = (MessageEventType.Opened, MessageStatus.Opened),
        ["email.failed"] = (MessageEventType.Failed, MessageStatus.Failed),
        ["email.bounced"] = (MessageEventType.Bounced, MessageStatus.Bounced),
        ["email.complained"] = (MessageEventType.Failed, MessageStatus.Failed)
    };

    public ResendWebhookProcessor(IAppDbContext db, ILogger<ResendWebhookProcessor> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<bool> ProcessAsync(string payload, CancellationToken cancellationToken)
    {
        using var activity = TelemetrySources.Webhooks.StartActivity("webhook.resend.handle", ActivityKind.Server);
        activity?.SetTag("webhook.provider", ProviderName);
        activity?.SetTag("webhook.payload_size", payload?.Length ?? 0);

        if (string.IsNullOrWhiteSpace(payload))
        {
            activity?.SetStatus(ActivityStatusCode.Error, "empty_payload");
            return false;
        }

        JsonDocument? doc = null;
        try
        {
            doc = JsonDocument.Parse(payload);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Unable to parse Resend webhook payload");
            activity?.SetStatus(ActivityStatusCode.Error, "invalid_json");
            return false;
        }

        using (doc)
        {
            var root = doc!.RootElement;
            if (!root.TryGetProperty("type", out var typeProp))
            {
                _logger.LogWarning("Resend webhook missing type");
                activity?.SetStatus(ActivityStatusCode.Error, "missing_type");
                return false;
            }

            var eventTypeName = typeProp.GetString();
            if (eventTypeName is null || !EventMap.TryGetValue(eventTypeName, out var mapping))
            {
                _logger.LogInformation("Ignoring Resend event {Type}", eventTypeName);
                activity?.SetTag("webhook.event", eventTypeName ?? "unknown");
                activity?.SetStatus(ActivityStatusCode.Ok, "ignored");
                return true;
            }

            if (!root.TryGetProperty("data", out var dataElement))
            {
                _logger.LogWarning("Resend webhook missing data section");
                activity?.SetStatus(ActivityStatusCode.Error, "missing_data");
                return false;
            }

            var providerMessageId = ExtractProviderMessageId(dataElement);
            if (string.IsNullOrWhiteSpace(providerMessageId))
            {
                _logger.LogWarning("Resend webhook missing message identifier for event {Type}", eventTypeName);
                activity?.SetStatus(ActivityStatusCode.Error, "missing_message_id");
                return false;
            }

            activity?.SetTag("webhook.event", eventTypeName);
            activity?.SetTag("webhook.provider_message_id", providerMessageId);

            var message = await _db.Messages.FirstOrDefaultAsync(m => m.ProviderMessageId == providerMessageId, cancellationToken);
            if (message is null)
            {
                _logger.LogWarning("Resend webhook could not match message id {ProviderMessageId}", providerMessageId);
                activity?.SetStatus(ActivityStatusCode.Ok, "message_not_found");
                return true;
            }

            var eventId = ExtractProviderEventId(root, dataElement, eventTypeName, providerMessageId);
            if (!string.IsNullOrWhiteSpace(eventId))
            {
                var exists = await _db.MessageEvents.AnyAsync(e => e.Provider == ProviderName && e.ProviderEventId == eventId, cancellationToken);
                if (exists)
                {
                    _logger.LogDebug("Resend event {EventId} already processed", eventId);
                    activity?.SetStatus(ActivityStatusCode.Ok, "duplicate");
                    return true;
                }
            }

            var occurredAt = ExtractTimestamp(root, dataElement);

            var messageEvent = new MessageEvent
            {
                Id = Guid.NewGuid(),
                TenantId = message.TenantId,
                MessageId = message.Id,
                Type = mapping.EventType,
                Provider = ProviderName,
                ProviderEventId = eventId,
                Raw = payload,
                CreatedAt = occurredAt ?? DateTime.UtcNow
            };

            await _db.MessageEvents.AddAsync(messageEvent, cancellationToken);

            ApplyStatusUpdate(message, mapping.Status, occurredAt, dataElement);

            await _db.SaveChangesAsync(cancellationToken);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return true;
        }
    }

    private static string? ExtractProviderMessageId(JsonElement data)
    {
        if (data.TryGetProperty("email_id", out var emailId) && emailId.ValueKind == JsonValueKind.String)
        {
            return emailId.GetString();
        }

        if (data.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
        {
            return id.GetString();
        }

        if (data.TryGetProperty("message_id", out var messageId) && messageId.ValueKind == JsonValueKind.String)
        {
            return messageId.GetString();
        }

        return null;
    }

    private static string ExtractProviderEventId(JsonElement root, JsonElement data, string eventType, string providerMessageId)
    {
        if (root.TryGetProperty("id", out var rootId) && rootId.ValueKind == JsonValueKind.String)
        {
            return rootId.GetString()!;
        }

        if (data.TryGetProperty("event_id", out var eventId) && eventId.ValueKind == JsonValueKind.String)
        {
            return eventId.GetString()!;
        }

        if (data.TryGetProperty("id", out var dataId) && dataId.ValueKind == JsonValueKind.String)
        {
            return $"{eventType}:{dataId.GetString()}";
        }

        return $"{eventType}:{providerMessageId}";
    }

    private static DateTime? ExtractTimestamp(JsonElement root, JsonElement data)
    {
        if (root.TryGetProperty("created_at", out var createdAt) && createdAt.ValueKind == JsonValueKind.String && DateTime.TryParse(createdAt.GetString(), out var createdAtValue))
        {
            return DateTime.SpecifyKind(createdAtValue, DateTimeKind.Utc);
        }

        if (data.TryGetProperty("created_at", out var dataCreated) && dataCreated.ValueKind == JsonValueKind.String && DateTime.TryParse(dataCreated.GetString(), out var dataCreatedValue))
        {
            return DateTime.SpecifyKind(dataCreatedValue, DateTimeKind.Utc);
        }

        if (data.TryGetProperty("delivered_at", out var deliveredAt) && deliveredAt.ValueKind == JsonValueKind.String && DateTime.TryParse(deliveredAt.GetString(), out var deliveredValue))
        {
            return DateTime.SpecifyKind(deliveredValue, DateTimeKind.Utc);
        }

        if (data.TryGetProperty("timestamp", out var timestamp) && timestamp.ValueKind == JsonValueKind.String && DateTime.TryParse(timestamp.GetString(), out var tsValue))
        {
            return DateTime.SpecifyKind(tsValue, DateTimeKind.Utc);
        }

        return null;
    }

    private void ApplyStatusUpdate(Message message, MessageStatus newStatus, DateTime? occurredAt, JsonElement data)
    {
        if (ShouldUpdateStatus(message.Status, newStatus))
        {
            message.Status = newStatus;
        }

        var timestamp = occurredAt ?? DateTime.UtcNow;

        switch (newStatus)
        {
            case MessageStatus.Delivered:
                message.DeliveredAt ??= timestamp;
                break;
            case MessageStatus.Opened:
                message.OpenedAt ??= timestamp;
                break;
            case MessageStatus.Failed:
                message.FailedAt ??= timestamp;
                message.ErrorCode ??= TryGetNestedString(data, "error", "code");
                message.ErrorReason = TryGetNestedString(data, "error", "message") ?? TryGetString(data, "reason") ?? message.ErrorReason;
                break;
            case MessageStatus.Bounced:
                message.FailedAt ??= timestamp;
                message.ErrorCode ??= TryGetNestedString(data, "bounce", "type");
                message.ErrorReason = TryGetNestedString(data, "bounce", "description") ?? TryGetString(data, "reason") ?? message.ErrorReason;
                break;
        }
    }

    private static bool ShouldUpdateStatus(MessageStatus current, MessageStatus candidate)
    {
        if (candidate == MessageStatus.Failed || candidate == MessageStatus.Bounced)
        {
            return true;
        }

        return candidate > current;
    }

    private static string? TryGetString(JsonElement element, string property)
    {
        if (element.TryGetProperty(property, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString();
        }

        return null;
    }

    private static string? TryGetNestedString(JsonElement element, string property, string nested)
    {
        if (element.TryGetProperty(property, out var parent) && parent.ValueKind == JsonValueKind.Object)
        {
            if (parent.TryGetProperty(nested, out var child) && child.ValueKind == JsonValueKind.String)
            {
                return child.GetString();
            }
        }

        return null;
    }
}

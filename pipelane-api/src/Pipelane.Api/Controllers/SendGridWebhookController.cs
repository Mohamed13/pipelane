using System.Text.Json;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Pipelane.Application.Storage;
using Pipelane.Domain.Entities.Prospecting;
using Pipelane.Domain.Enums.Prospecting;

namespace Pipelane.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/email/webhooks/sendgrid")]
public class SendGridWebhookController : ControllerBase
{
    private readonly IAppDbContext _db;
    private readonly ILogger<SendGridWebhookController> _logger;

    public SendGridWebhookController(IAppDbContext db, ILogger<SendGridWebhookController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Handle([FromBody] JsonElement payload, CancellationToken ct)
    {
        if (payload.ValueKind != JsonValueKind.Array)
        {
            _logger.LogWarning("Unexpected SendGrid payload format: {Kind}", payload.ValueKind);
            return BadRequest();
        }

        foreach (var evt in payload.EnumerateArray())
        {
            await HandleEventAsync(evt, ct);
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new { received = payload.GetArrayLength() });
    }

    private async Task HandleEventAsync(JsonElement evt, CancellationToken ct)
    {
        var eventType = evt.GetPropertyOrDefault("event") ?? evt.GetPropertyOrDefault("type");
        if (string.IsNullOrWhiteSpace(eventType))
        {
            return;
        }

        eventType = eventType.ToLowerInvariant();
        if (eventType == "inbound")
        {
            await HandleInboundAsync(evt, ct);
            return;
        }

        var sendLogId = ExtractSendLogId(evt);
        SendLog? log = null;
        if (sendLogId.HasValue)
        {
            log = await _db.ProspectingSendLogs.FirstOrDefaultAsync(l => l.Id == sendLogId.Value, ct);
        }

        var providerMessageId = evt.GetPropertyOrDefault("sg_message_id") ?? evt.GetPropertyOrDefault("smtp-id");
        if (log == null && !string.IsNullOrWhiteSpace(providerMessageId))
        {
            log = await _db.ProspectingSendLogs.FirstOrDefaultAsync(l => l.ProviderMessageId == providerMessageId, ct);
        }

        if (log == null)
        {
            _logger.LogDebug("SendGrid event {EventType} could not be matched to a send log. sg_message_id={MessageId}", eventType, providerMessageId);
            return;
        }

        var timestampSeconds = evt.GetPropertyOrDefault("timestamp");
        var timestamp = DateTime.UtcNow;
        if (long.TryParse(timestampSeconds, out var seconds))
        {
            timestamp = DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;
        }

        switch (eventType)
        {
            case "processed":
            case "queued":
                log.Status = SendLogStatus.PendingSend;
                log.UpdatedAtUtc = timestamp;
                break;
            case "delivered":
                log.Status = SendLogStatus.Delivered;
                log.DeliveredAtUtc = timestamp;
                log.UpdatedAtUtc = timestamp;
                break;
            case "open":
                log.Status = SendLogStatus.Opened;
                log.OpenedAtUtc = timestamp;
                log.UpdatedAtUtc = timestamp;
                break;
            case "click":
                log.Status = SendLogStatus.Clicked;
                log.ClickedAtUtc = timestamp;
                log.UpdatedAtUtc = timestamp;
                break;
            case "bounce":
            case "dropped":
                log.Status = SendLogStatus.Bounced;
                log.BouncedAtUtc = timestamp;
                log.ErrorReason = evt.GetPropertyOrDefault("reason");
                log.ErrorCode = evt.GetPropertyOrDefault("smtp-id");
                log.UpdatedAtUtc = timestamp;
                break;
            case "spamreport":
                log.Status = SendLogStatus.Complained;
                log.ComplainedAtUtc = timestamp;
                log.UpdatedAtUtc = timestamp;
                break;
            default:
                _logger.LogDebug("Unhandled SendGrid event type {EventType}", eventType);
                break;
        }
    }

    private async Task HandleInboundAsync(JsonElement evt, CancellationToken ct)
    {
        var from = evt.GetPropertyOrDefault("from") ?? string.Empty;
        var email = from.Contains('<') ? from.Split('<', '>')[1] : evt.GetPropertyOrDefault("email") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        var normalized = email.Trim().ToLowerInvariant();
        var prospect = await _db.Prospects.FirstOrDefaultAsync(p => p.Email.ToLower() == normalized, ct);
        if (prospect is null)
        {
            _logger.LogDebug("Inbound email from {Email} has no matching prospect.", normalized);
            return;
        }

        var sendLogId = ExtractSendLogId(evt);
        Guid? stepId = null;
        Guid? campaignId = null;
        if (sendLogId.HasValue)
        {
            var log = await _db.ProspectingSendLogs.FirstOrDefaultAsync(l => l.Id == sendLogId.Value, ct);
            if (log != null)
            {
                stepId = log.StepId;
                campaignId = log.CampaignId;
            }
        }

        var reply = new ProspectReply
        {
            Id = Guid.NewGuid(),
            TenantId = prospect.TenantId,
            ProspectId = prospect.Id,
            CampaignId = campaignId,
            StepId = stepId,
            SendLogId = sendLogId,
            Provider = "sendgrid",
            ProviderMessageId = evt.GetPropertyOrDefault("smtp-id"),
            ReceivedAtUtc = DateTime.UtcNow,
            Subject = evt.GetPropertyOrDefault("subject"),
            TextBody = evt.GetPropertyOrDefault("text"),
            HtmlBody = evt.GetPropertyOrDefault("html"),
            Intent = ReplyIntent.Unknown,
            CreatedAtUtc = DateTime.UtcNow,
            MetadataJson = evt.ToString()
        };

        await _db.ProspectReplies.AddAsync(reply, ct);
        prospect.LastRepliedAtUtc = reply.ReceivedAtUtc;
        prospect.Status = ReplyIntentToStatus(reply.Intent);
    }

    private static Guid? ExtractSendLogId(JsonElement evt)
    {
        if (evt.TryGetProperty("sendLogId", out var direct) && Guid.TryParse(direct.GetString(), out var directId))
        {
            return directId;
        }

        if (evt.TryGetProperty("custom_args", out var customArgs) && customArgs.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in customArgs.EnumerateObject())
            {
                if ((prop.NameEquals("sendLogId") || prop.NameEquals("sendlogid")) && Guid.TryParse(prop.Value.GetString(), out var parsed))
                {
                    return parsed;
                }
            }
        }

        return null;
    }

    private static ProspectStatus ReplyIntentToStatus(ReplyIntent intent) =>
        intent switch
        {
            ReplyIntent.Interested => ProspectStatus.Replied,
            ReplyIntent.MeetingRequested => ProspectStatus.MeetingBooked,
            ReplyIntent.Unsubscribe => ProspectStatus.OptedOut,
            ReplyIntent.NotInterested => ProspectStatus.Replied,
            _ => ProspectStatus.Active
        };
}

file static class JsonElementSendGridExtensions
{
    public static string? GetPropertyOrDefault(this JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out var property) && property.ValueKind != JsonValueKind.Null)
        {
            return property.ToString();
        }
        return null;
    }

    public static bool NameEquals(this JsonProperty property, string candidate) =>
        property.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase);
}

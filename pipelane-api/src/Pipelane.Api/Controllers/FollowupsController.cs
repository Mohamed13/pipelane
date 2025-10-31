using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Pipelane.Application.Abstractions;
using Pipelane.Application.Ai;
using Pipelane.Application.Common;
using Pipelane.Application.DTOs;
using Pipelane.Application.Services;
using Pipelane.Application.Storage;
using Pipelane.Domain.Entities;
using Pipelane.Domain.Enums;
using Pipelane.Infrastructure.Background;
using Pipelane.Infrastructure.Persistence;

namespace Pipelane.Api.Controllers;

[ApiController]
[Microsoft.AspNetCore.Authorization.Authorize]
[Route("api/followups")]
public sealed class FollowupsController : ControllerBase
{
    private readonly IAppDbContext _db;
    private readonly IOutboxService _outbox;
    private readonly IFollowupProposalStore _proposalStore;
    private readonly ITenantProvider _tenantProvider;
    private readonly ITextAiService _aiService;
    private readonly MessagingLimitsOptions _messagingLimits;
    private readonly TimeProvider _clock;
    private readonly ILogger<FollowupsController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public FollowupsController(
        IAppDbContext db,
        IOutboxService outbox,
        IFollowupProposalStore proposalStore,
        ITenantProvider tenantProvider,
        ITextAiService aiService,
        IOptions<MessagingLimitsOptions> messagingOptions,
        ILogger<FollowupsController> logger,
        TimeProvider? clock = null)
    {
        _db = db;
        _outbox = outbox;
        _proposalStore = proposalStore;
        _tenantProvider = tenantProvider;
        _aiService = aiService;
        _messagingLimits = messagingOptions.Value;
        _logger = logger;
        _clock = clock ?? TimeProvider.System;
    }

    [HttpGet("preview")]
    public async Task<ActionResult<FollowupConversationPreviewResponse>> PreviewConversation([FromQuery] Guid conversationId, CancellationToken ct)
    {
        if (conversationId == Guid.Empty)
        {
            return BadRequest(CreateConversationIdProblem());
        }

        return await PreviewConversationInternal(conversationId, ct).ConfigureAwait(false);
    }

    [HttpPost("preview")]
    public async Task<ActionResult<object>> Preview([FromBody] FollowupPreviewRequest? request, CancellationToken cancellationToken)
    {
        using var activity = TelemetrySources.Followups.StartActivity("followup.preview.request", ActivityKind.Server);
        activity?.SetTag("followup.tenant_id", _tenantProvider.TenantId);

        var effectiveRequest = request ?? new FollowupPreviewRequest();
        effectiveRequest.SegmentJson = string.IsNullOrWhiteSpace(effectiveRequest.SegmentJson)
            ? "{}"
            : effectiveRequest.SegmentJson;

        if (effectiveRequest.ConversationId.HasValue)
        {
            var conversationId = effectiveRequest.ConversationId.Value;
            activity?.SetTag("followup.conversation_id", conversationId);
            if (conversationId == Guid.Empty)
            {
                activity?.SetStatus(ActivityStatusCode.Error, "invalid_conversation_id");
                return BadRequest(CreateConversationIdProblem());
            }

            var preview = await PreviewConversationInternal(conversationId, cancellationToken).ConfigureAwait(false);
            if (preview.Result is not null)
            {
                if (preview.Result is ObjectResult objectResult && objectResult.StatusCode >= 400)
                {
                    activity?.SetStatus(ActivityStatusCode.Error, $"child_{objectResult.StatusCode}");
                }
                return preview.Result;
            }

            activity?.SetStatus(ActivityStatusCode.Ok);
            return Ok(preview.Value!);
        }

        var query = _db.Contacts.AsQueryable();

        if (!string.IsNullOrWhiteSpace(effectiveRequest.SegmentJson))
        {
            try
            {
                using var segmentDoc = JsonDocument.Parse(effectiveRequest.SegmentJson);
                var root = segmentDoc.RootElement;

                if (root.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                {
                    // no-op: treat null/undefined as empty filter
                }
                else if (root.ValueKind != JsonValueKind.Object)
                {
                    activity?.SetStatus(ActivityStatusCode.Error, "invalid_segment_json_shape");
                    return BadRequest("SegmentJson must be a JSON object");
                }
                else
                {
                    var lang = TryGetString(root, "lang");
                    if (!string.IsNullOrWhiteSpace(lang))
                    {
                        query = query.Where(c => c.Lang == lang);
                    }

                    var tags = ExtractTags(root);
                    if (tags.Length > 0)
                    {
                        query = ApplyTagFilter(query, tags);
                    }
                }
            }
            catch (JsonException)
            {
                activity?.SetStatus(ActivityStatusCode.Error, "invalid_segment_json");
                return BadRequest("SegmentJson is not valid JSON");
            }
        }

        var count = await query.CountAsync(cancellationToken);
        activity?.SetTag("followup.segment_count", count);
        activity?.SetStatus(ActivityStatusCode.Ok);
        return Ok(new { count });
    }

    [HttpPost("validate")]
    public async Task<ActionResult<object>> Validate([FromBody] ValidateFollowupRequest request, CancellationToken ct)
    {
        var tenantId = _tenantProvider.TenantId;
        using var activity = TelemetrySources.Followups.StartActivity("followup.validate", ActivityKind.Server);
        activity?.SetTag("followup.tenant_id", tenantId);
        activity?.SetTag("followup.proposal_id", request.ProposalId);
        activity?.SetTag("followup.conversation_id", request.ConversationId);
        activity?.SetTag("followup.send_now", request.SendNow);
        if (tenantId == Guid.Empty)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "tenant_missing");
            return BadRequest("tenant_missing");
        }

        if (!_proposalStore.TryGet(tenantId, request.ProposalId, out var proposal) || proposal is null)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "proposal_not_found");
            return NotFound("proposal_not_found");
        }

        var conversation = await _db.Conversations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.ConversationId, ct);

        if (conversation is null)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "conversation_not_found");
            return NotFound("conversation_not_found");
        }

        var contact = await _db.Contacts
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == conversation.ContactId, ct);

        if (contact is null)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "contact_not_found");
            return NotFound("contact_not_found");
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        DateTime? scheduledUtc = request.SendNow ? null : proposal.ScheduledAtUtc;
        if (!request.SendNow && scheduledUtc.HasValue && scheduledUtc.Value <= now)
        {
            scheduledUtc = now.AddMinutes(5);
        }

        var payload = new Dictionary<string, string>
        {
            ["text"] = proposal.PreviewText ?? string.Empty
        };

        var meta = new Dictionary<string, string>
        {
            ["angle"] = proposal.Angle.ToString().ToLowerInvariant(),
            ["language"] = proposal.Language ?? string.Empty
        };

        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ContactId = contact.Id,
            ConversationId = conversation.Id,
            Channel = proposal.Channel,
            Type = MessageType.Text,
            PayloadJson = JsonSerializer.Serialize(payload, _jsonOptions),
            MetaJson = JsonSerializer.Serialize(meta, _jsonOptions),
            ScheduledAtUtc = scheduledUtc,
            CreatedAt = now
        };

        await _outbox.EnqueueAsync(outboxMessage, ct);
        _proposalStore.Remove(tenantId, request.ProposalId);

        _logger.LogInformation("Follow-up validated tenant={TenantId} conversation={ConversationId} proposal={ProposalId} scheduledAt={ScheduledAt}",
            tenantId,
            conversation.Id,
            request.ProposalId,
            scheduledUtc ?? now);

        activity?.SetTag("followup.scheduled_utc", scheduledUtc ?? now);
        activity?.SetStatus(ActivityStatusCode.Ok);

        return Ok(new
        {
            scheduledAt = scheduledUtc ?? now,
            conversationId = conversation.Id
        });
    }

    private string BuildHistorySnippet(IReadOnlyList<Message> messages, int limit)
    {
        if (messages.Count == 0)
        {
            return string.Empty;
        }

        var builder = new List<string>();
        foreach (var message in messages.Skip(Math.Max(0, messages.Count - limit)))
        {
            var who = message.Direction == MessageDirection.In ? "Client" : "Vous";
            var text = ExtractText(message.PayloadJson);
            if (string.IsNullOrWhiteSpace(text))
            {
                text = message.TemplateName ?? message.Type.ToString();
            }
            builder.Add($"{who}: {text.Trim()}");
        }

        return string.Join(Environment.NewLine, builder);
    }

    private static string ExtractText(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return string.Empty;
        }

        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.String)
            {
                return root.GetString() ?? string.Empty;
            }

            if (root.TryGetProperty("text", out var textProp) && textProp.ValueKind == JsonValueKind.String)
            {
                return textProp.GetString() ?? string.Empty;
            }

            if (root.TryGetProperty("body", out var bodyProp) && bodyProp.ValueKind == JsonValueKind.String)
            {
                return bodyProp.GetString() ?? string.Empty;
            }

            if (root.TryGetProperty("title", out var titleProp) && titleProp.ValueKind == JsonValueKind.String)
            {
                return titleProp.GetString() ?? string.Empty;
            }
        }
        catch (JsonException)
        {
            return payloadJson;
        }

        return payloadJson;
    }

    private static string ResolveTimezone(string? lang)
    {
        if (string.IsNullOrWhiteSpace(lang))
        {
            return TimeZoneInfo.Utc.Id;
        }

        return lang.Equals("fr", StringComparison.OrdinalIgnoreCase)
            ? "Europe/Paris"
            : TimeZoneInfo.Utc.Id;
    }

    private async Task<DateTime> EnforceFollowupLimitsAsync(Guid tenantId, string timezone, DateTime scheduledUtc, CancellationToken ct)
    {
        var tz = ResolveTimezoneInfo(timezone);
        var sanitized = EnsureFuture(scheduledUtc);
        sanitized = AdjustForQuietHours(sanitized, tz);
        sanitized = EnsureFuture(sanitized);

        if (tenantId != Guid.Empty)
        {
            var dayStartUtc = _clock.GetUtcNow().UtcDateTime.Date;
            var sentToday = await _db.Messages
                .Where(m => m.TenantId == tenantId && m.Direction == MessageDirection.Out && m.CreatedAt >= dayStartUtc && m.Status != MessageStatus.Failed)
                .CountAsync(ct)
                .ConfigureAwait(false);

            if (sentToday >= _messagingLimits.DailySendCap)
            {
                sanitized = MoveToNextWindow(sanitized, tz);
            }
        }

        return EnsureFuture(sanitized);
    }

    private static TimeZoneInfo ResolveTimezoneInfo(string timezone)
    {
        if (string.IsNullOrWhiteSpace(timezone))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timezone);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    private DateTime AdjustForQuietHours(DateTime scheduledUtc, TimeZoneInfo tz)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(scheduledUtc, tz);
        var start = _messagingLimits.QuietHoursStart;
        var end = _messagingLimits.QuietHoursEnd;

        var inQuiet = start <= end
            ? local.TimeOfDay >= start && local.TimeOfDay < end
            : local.TimeOfDay >= start || local.TimeOfDay < end;

        if (!inQuiet)
        {
            return scheduledUtc;
        }

        var midnight = new DateTime(local.Year, local.Month, local.Day, 0, 0, 0, local.Kind);
        var nextLocal = midnight.Add(end);
        if (local.TimeOfDay >= start)
        {
            nextLocal = nextLocal.AddDays(1);
        }

        return TimeZoneInfo.ConvertTimeToUtc(nextLocal, tz);
    }

    private DateTime MoveToNextWindow(DateTime scheduledUtc, TimeZoneInfo tz)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(scheduledUtc, tz).AddDays(1);
        var target = new DateTime(local.Year, local.Month, local.Day, 10, 30, 0, local.Kind);
        return TimeZoneInfo.ConvertTimeToUtc(target, tz);
    }

    private DateTime EnsureFuture(DateTime scheduledUtc)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        if (scheduledUtc <= now.AddMinutes(5))
        {
            return now.AddMinutes(5);
        }

        return scheduledUtc;
    }

    private static string MapAngle(AiFollowupAngle angle) => angle switch
    {
        AiFollowupAngle.Reminder => "reminder",
        AiFollowupAngle.Value => "value",
        AiFollowupAngle.Social => "social",
        AiFollowupAngle.Question => "question",
        _ => "reminder"
    };

    public sealed record ValidateFollowupRequest(
        [property: JsonPropertyName("conversationId")] Guid ConversationId,
        [property: JsonPropertyName("proposalId")] Guid ProposalId,
        [property: JsonPropertyName("sendNow")] bool SendNow = false);

    public sealed record FollowupConversationPreviewResponse(
        [property: JsonPropertyName("historySnippet")] string HistorySnippet,
        [property: JsonPropertyName("lastInteractionAt")] DateTime LastInteractionAt,
        [property: JsonPropertyName("read")] bool Read,
        [property: JsonPropertyName("timezone")] string Timezone,
        [property: JsonPropertyName("proposal")] FollowupProposalPreview Proposal);

    public sealed record FollowupProposalPreview(
        [property: JsonPropertyName("proposalId")] Guid ProposalId,
        [property: JsonPropertyName("scheduledAtIso")] string ScheduledAtIso,
        [property: JsonPropertyName("angle")] string Angle,
        [property: JsonPropertyName("previewText")] string PreviewText);

    private async Task<ActionResult<FollowupConversationPreviewResponse>> PreviewConversationInternal(Guid conversationId, CancellationToken ct)
    {
        using var activity = TelemetrySources.Followups.StartActivity("followup.preview.conversation", ActivityKind.Server);
        activity?.SetTag("followup.conversation_id", conversationId);

        var tenantId = _tenantProvider.TenantId;
        activity?.SetTag("followup.tenant_id", tenantId);

        var conversation = await _db.Conversations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == conversationId, ct)
            .ConfigureAwait(false);

        if (conversation is null || (tenantId != Guid.Empty && conversation.TenantId != tenantId))
        {
            _logger.LogWarning("Conversation {ConversationId} not found for tenant {TenantId}", conversationId, tenantId);
            activity?.SetStatus(ActivityStatusCode.Error, "conversation_not_found");
            return NotFound("conversation_not_found");
        }

        var messages = await _db.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (messages.Count == 0)
        {
            _logger.LogWarning("Conversation {ConversationId} has no messages, cannot preview follow-up", conversationId);
            activity?.SetStatus(ActivityStatusCode.Error, "no_messages");
            return NotFound("no_messages");
        }

        var contact = await _db.Contacts
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == conversation.ContactId, ct)
            .ConfigureAwait(false);

        var historySnippet = BuildHistorySnippet(messages, 6);
        var lastMessage = messages[^1];
        var read = messages.Any(m => m.Status == MessageStatus.Opened);
        var timezone = ResolveTimezone(contact?.Lang);
        var language = contact?.Lang ?? lastMessage.Lang ?? "en";

        var suggestion = await _aiService.SuggestFollowupAsync(
            tenantId,
            new SuggestFollowupCommand(
                conversation.PrimaryChannel,
                timezone,
                lastMessage.CreatedAt,
                read,
                language,
                historySnippet,
                null),
            ct).ConfigureAwait(false);

        var scheduledUtc = await EnforceFollowupLimitsAsync(tenantId, timezone, suggestion.ScheduledAtUtc, ct).ConfigureAwait(false);
        var proposalId = _proposalStore.Save(tenantId, new FollowupProposalData(
            conversation.PrimaryChannel,
            scheduledUtc,
            suggestion.Angle,
            suggestion.PreviewText,
            language));

        var resolvedTimezone = ResolveTimezoneInfo(timezone);
        var scheduledLocal = TimeZoneInfo.ConvertTimeFromUtc(scheduledUtc, resolvedTimezone);

        _logger.LogInformation(
            "Preview follow-up computed for tenant {TenantId} conversation {ConversationId} hasHistory={HasHistory} scheduledUtc={ScheduledUtc:o} scheduledLocal={ScheduledLocal:o}",
            tenantId,
            conversationId,
            messages.Count > 0,
            scheduledUtc,
            scheduledLocal);

        activity?.SetTag("followup.scheduled_utc", scheduledUtc);
        activity?.SetTag("followup.angle", suggestion.Angle.ToString());
        activity?.SetStatus(ActivityStatusCode.Ok);

        return Ok(new FollowupConversationPreviewResponse(
            historySnippet,
            lastMessage.CreatedAt,
            read,
            timezone,
            new FollowupProposalPreview(
                proposalId,
                scheduledUtc.ToString("o"),
                MapAngle(suggestion.Angle),
                suggestion.PreviewText)));
    }

    private static ProblemDetails CreateConversationIdProblem() => new()
    {
        Title = "invalid_request",
        Detail = "conversationId required",
        Status = StatusCodes.Status400BadRequest
    };

    private static string? TryGetString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static string[] ExtractTags(JsonElement root)
    {
        var tags = new List<string>();

        if (root.TryGetProperty("tag", out var tagElement) && tagElement.ValueKind == JsonValueKind.String)
        {
            var value = tagElement.GetString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                tags.Add(value);
            }
        }

        if (root.TryGetProperty("tags", out var tagsElement))
        {
            switch (tagsElement.ValueKind)
            {
                case JsonValueKind.String:
                    var single = tagsElement.GetString();
                    if (!string.IsNullOrWhiteSpace(single))
                    {
                        tags.Add(single);
                    }
                    break;
                case JsonValueKind.Array:
                    foreach (var item in tagsElement.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String)
                        {
                            var arrayValue = item.GetString();
                            if (!string.IsNullOrWhiteSpace(arrayValue))
                            {
                                tags.Add(arrayValue);
                            }
                        }
                    }
                    break;
            }
        }

        return tags
            .Select(tag => tag.Trim())
            .Where(tag => !string.IsNullOrEmpty(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IQueryable<Contact> ApplyTagFilter(IQueryable<Contact> source, IReadOnlyList<string> tags)
    {
        if (tags.Count == 0)
        {
            return source;
        }

        var parameter = Expression.Parameter(typeof(Contact), "contact");
        var tagsJsonProperty = Expression.Property(parameter, nameof(Contact.TagsJson));
        var nullConstant = Expression.Constant(null, typeof(string));
        var containsMethod = typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) })!;
        Expression? body = null;

        foreach (var tag in tags)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                continue;
            }

            var tagConstant = Expression.Constant(tag, typeof(string));
            var notNull = Expression.NotEqual(tagsJsonProperty, nullConstant);
            var containsCall = Expression.Call(tagsJsonProperty, containsMethod, tagConstant);
            var predicate = Expression.AndAlso(notNull, containsCall);
            body = body is null ? predicate : Expression.OrElse(body, predicate);
        }

        if (body is null)
        {
            return source;
        }

        var lambda = Expression.Lambda<Func<Contact, bool>>(body, parameter);
        return source.Where(lambda);
    }
}

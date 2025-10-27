using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Pipelane.Application.Ai;
using Pipelane.Application.Services;
using Pipelane.Application.Storage;
using Pipelane.Domain.Enums;
using Pipelane.Infrastructure.Background;
using Pipelane.Infrastructure.Persistence;

namespace Pipelane.Api.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize]
[EnableRateLimiting("tenant-ai")]
public sealed class AiController : ControllerBase
{
    private static readonly string[] OptOutSignals = ["stop", "unsubscribe", "opt out", "opt-out", "remove me"];

    private readonly ITextAiService _aiService;
    private readonly ITenantProvider _tenantProvider;
    private readonly IAppDbContext _db;
    private readonly ILogger<AiController> _logger;
    private readonly IFollowupProposalStore _proposalStore;
    private readonly MessagingLimitsOptions _messagingLimits;
    private readonly TimeProvider _clock;
    public AiController(
        ITextAiService aiService,
        ITenantProvider tenantProvider,
        IAppDbContext db,
        ILogger<AiController> logger,
        IFollowupProposalStore proposalStore,
        IOptions<MessagingLimitsOptions> messagingOptions,
        TimeProvider? clock = null)
    {
        _aiService = aiService;
        _tenantProvider = tenantProvider;
        _db = db;
        _logger = logger;
        _proposalStore = proposalStore;
        _messagingLimits = messagingOptions.Value;
        _clock = clock ?? TimeProvider.System;
    }

    [HttpPost("generate-message")]
    public async Task<ActionResult<GenerateMessageResponse>> GenerateMessage(
        [FromBody] GenerateMessageRequest request,
        CancellationToken ct)
    {
        try
        {
            if (await IsOptOutAsync(request.Channel, request.ContactId, request.Context.LastMessageSnippet, ct).ConfigureAwait(false))
            {
                return Conflict(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Opt-out detected",
                    Detail = "This contact requested to stop receiving messages."
                });
            }

            var tenantId = _tenantProvider.TenantId;
            var command = new GenerateMessageCommand(
                request.ContactId,
                request.Channel,
                request.Language,
                new AiMessageContext(
                    request.Context.FirstName,
                    request.Context.LastName,
                    request.Context.Company,
                    request.Context.Role,
                    request.Context.PainPoints,
                    request.Context.Pitch,
                    request.Context.CalendlyUrl,
                    request.Context.LastMessageSnippet));

            var result = await _aiService.GenerateMessageAsync(tenantId, command, ct).ConfigureAwait(false);
            return Ok(new GenerateMessageResponse(result.Subject, result.Text, result.Html, result.LanguageDetected));
        }
        catch (AiDisabledException ex)
        {
            _logger.LogWarning(ex, "AI service disabled for tenant {TenantId}", _tenantProvider.TenantId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "AI service unavailable",
                Detail = ex.Message
            });
        }
        catch (AiBudgetExceededException ex)
        {
            _logger.LogWarning(ex, "AI budget exceeded for tenant {TenantId}", _tenantProvider.TenantId);
            return StatusCode(StatusCodes.Status429TooManyRequests, new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "AI daily budget exceeded",
                Detail = ex.Message
            });
        }
        catch (ArgumentException ex)
        {
            return ValidationProblem(new ValidationProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Detail = ex.Message
            });
        }
    }

    [HttpPost("classify-reply")]
    public async Task<ActionResult<ClassifyReplyResponse>> ClassifyReply(
        [FromBody] ClassifyReplyRequest request,
        CancellationToken ct)
    {
        try
        {
            var tenantId = _tenantProvider.TenantId;
            var command = new ClassifyReplyCommand(request.Text, request.Language);
            var result = await _aiService.ClassifyReplyAsync(tenantId, command, ct).ConfigureAwait(false);

            return Ok(new ClassifyReplyResponse(MapIntent(result.Intent), result.Confidence));
        }
        catch (AiDisabledException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "AI service unavailable",
                Detail = ex.Message
            });
        }
        catch (AiBudgetExceededException ex)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "AI daily budget exceeded",
                Detail = ex.Message
            });
        }
        catch (ArgumentException ex)
        {
            return ValidationProblem(new ValidationProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Detail = ex.Message
            });
        }
    }

    [HttpPost("suggest-followup")]
    public async Task<ActionResult<SuggestFollowupResponse>> SuggestFollowup(
        [FromBody] SuggestFollowupRequest request,
        CancellationToken ct)
    {
        try
        {
            if (await IsOptOutAsync(request.Channel, request.ContactId, request.HistorySnippet, ct).ConfigureAwait(false))
            {
                return Conflict(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Opt-out detected",
                    Detail = "This contact requested to stop receiving messages."
                });
            }

            var tenantId = _tenantProvider.TenantId;
            var command = new SuggestFollowupCommand(
                request.Channel,
                request.Timezone,
                request.LastInteractionAt,
                request.Read,
                request.Language,
                request.HistorySnippet,
                request.PerformanceHints is null
                    ? null
                    : new AiPerformanceHints(
                        request.PerformanceHints.GoodHours,
                        request.PerformanceHints.BadDays));

            var result = await _aiService.SuggestFollowupAsync(tenantId, command, ct).ConfigureAwait(false);

            var scheduledUtc = await EnforceFollowupLimitsAsync(tenantId, request.Timezone, result.ScheduledAtUtc, ct).ConfigureAwait(false);
            var proposalId = _proposalStore.Save(tenantId, new FollowupProposalData(
                request.Channel,
                scheduledUtc,
                result.Angle,
                result.PreviewText,
                request.Language));

            return Ok(new SuggestFollowupResponse(
                proposalId,
                scheduledUtc.ToString("o"),
                MapAngle(result.Angle),
                result.PreviewText));
        }
        catch (AiDisabledException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "AI service unavailable",
                Detail = ex.Message
            });
        }
        catch (AiBudgetExceededException ex)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "AI daily budget exceeded",
                Detail = ex.Message
            });
        }
        catch (ArgumentException ex)
        {
            return ValidationProblem(new ValidationProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Detail = ex.Message
            });
        }
    }

    private async Task<DateTime> EnforceFollowupLimitsAsync(Guid tenantId, string timezone, DateTime scheduledUtc, CancellationToken ct)
    {
        var tz = ResolveTimezone(timezone);
        var sanitized = EnsureFuture(scheduledUtc);
        sanitized = AdjustForQuietHours(sanitized, tz);
        sanitized = EnsureFuture(sanitized);

        var dayStartUtc = _clock.GetUtcNow().UtcDateTime.Date;
        var sentToday = await _db.Messages
            .Where(m => m.TenantId == tenantId && m.Direction == MessageDirection.Out && m.CreatedAt >= dayStartUtc && m.Status != MessageStatus.Failed)
            .CountAsync(ct)
            .ConfigureAwait(false);

        if (sentToday >= _messagingLimits.DailySendCap)
        {
            sanitized = MoveToNextWindow(sanitized, tz);
        }

        return EnsureFuture(sanitized);
    }

    private static TimeZoneInfo ResolveTimezone(string timezone)
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

    private async Task<bool> IsOptOutAsync(Channel channel, Guid? contactId, string? contextSnippet, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(contextSnippet))
        {
            var lowered = contextSnippet.ToLowerInvariant();
            if (OptOutSignals.Any(signal => lowered.Contains(signal, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        if (contactId is null)
        {
            return false;
        }

        var contact = await _db.Contacts
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == contactId.Value, ct)
            .ConfigureAwait(false);

        if (contact is not null && !string.IsNullOrWhiteSpace(contact.TagsJson) && HasOptOutTag(contact.TagsJson, channel))
        {
            return true;
        }

        var prospect = await _db.Prospects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == contactId.Value, ct)
            .ConfigureAwait(false);

        if (prospect?.OptedOut == true)
        {
            return true;
        }

        return false;
    }

    private static bool HasOptOutTag(string tagsJson, Channel channel)
    {
        try
        {
            using var document = JsonDocument.Parse(tagsJson);
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                var tags = document.RootElement.EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String)
                    .Select(e => e.GetString()?.ToLowerInvariant())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();

                return channel switch
                {
                    Channel.Email => tags.Contains("optout_email") || tags.Contains("stop_email") || tags.Contains("unsubscribe") || tags.Contains("do_not_contact"),
                    Channel.Sms => tags.Contains("optout_sms") || tags.Contains("stop_sms") || tags.Contains("stop"),
                    Channel.Whatsapp => tags.Contains("optout_whatsapp") || tags.Contains("stop_whatsapp") || tags.Contains("stop"),
                    _ => false
                };
            }
        }
        catch (JsonException)
        {
        }

        return false;
    }

    private static string MapIntent(AiReplyIntent intent) => intent switch
    {
        AiReplyIntent.Interested => "Interested",
        AiReplyIntent.Maybe => "Maybe",
        AiReplyIntent.NotNow => "NotNow",
        AiReplyIntent.NotRelevant => "NotRelevant",
        AiReplyIntent.Ooo => "OOO",
        AiReplyIntent.AutoReply => "AutoReply",
        _ => "Maybe"
    };

    private static string MapAngle(AiFollowupAngle angle) => angle switch
    {
        AiFollowupAngle.Value => "value",
        AiFollowupAngle.Social => "social",
        AiFollowupAngle.Question => "question",
        _ => "reminder"
    };

    public sealed record GenerateMessageRequest
    {
        [JsonPropertyName("contactId")]
        public Guid? ContactId { get; init; }

        [JsonPropertyName("language")]
        public string? Language { get; init; }

        [JsonPropertyName("channel")]
        [Required]
        public Channel Channel { get; init; }

        [JsonPropertyName("context")]
        [Required]
        public GenerateMessageRequestContext Context { get; init; } = new();
    }

    public sealed record GenerateMessageRequestContext
    {
        [JsonPropertyName("firstName")]
        public string? FirstName { get; init; }

        [JsonPropertyName("lastName")]
        public string? LastName { get; init; }

        [JsonPropertyName("company")]
        public string? Company { get; init; }

        [JsonPropertyName("role")]
        public string? Role { get; init; }

        [JsonPropertyName("painPoints")]
        public IReadOnlyList<string>? PainPoints { get; init; }

        [JsonPropertyName("pitch")]
        [Required]
        public string Pitch { get; init; } = string.Empty;

        [JsonPropertyName("calendlyUrl")]
        public string? CalendlyUrl { get; init; }

        [JsonPropertyName("lastMessageSnippet")]
        public string? LastMessageSnippet { get; init; }
    }

    public sealed record GenerateMessageResponse(
        [property: JsonPropertyName("subject")] string? Subject,
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("html")] string? Html,
        [property: JsonPropertyName("languageDetected")] string LanguageDetected);

    public sealed record ClassifyReplyRequest
    {
        [JsonPropertyName("text")]
        [Required]
        public string Text { get; init; } = string.Empty;

        [JsonPropertyName("language")]
        public string? Language { get; init; }
    }

    public sealed record ClassifyReplyResponse(
        [property: JsonPropertyName("intent")] string Intent,
        [property: JsonPropertyName("confidence")] double Confidence);

    public sealed record SuggestFollowupRequest
    {
        [JsonPropertyName("contactId")]
        public Guid? ContactId { get; init; }

        [JsonPropertyName("channel")]
        [Required]
        public Channel Channel { get; init; }

        [JsonPropertyName("timezone")]
        [Required]
        public string Timezone { get; init; } = "UTC";

        [JsonPropertyName("lastInteractionAt")]
        [Required]
        public DateTime LastInteractionAt { get; init; }

        [JsonPropertyName("read")]
        public bool Read { get; init; }

        [JsonPropertyName("language")]
        public string? Language { get; init; }

        [JsonPropertyName("historySnippet")]
        public string? HistorySnippet { get; init; }

        [JsonPropertyName("performanceHints")]
        public SuggestFollowupPerformanceHints? PerformanceHints { get; init; }
    }

    public sealed record SuggestFollowupPerformanceHints
    {
        [JsonPropertyName("goodHours")]
        public IReadOnlyList<int>? GoodHours { get; init; }

        [JsonPropertyName("badDays")]
        public IReadOnlyList<string>? BadDays { get; init; }
    }

    public sealed record SuggestFollowupResponse(
        [property: JsonPropertyName("proposalId")] Guid ProposalId,
        [property: JsonPropertyName("scheduledAtIso")] string ScheduledAtIso,
        [property: JsonPropertyName("angle")] string Angle,
        [property: JsonPropertyName("previewText")] string PreviewText);
}

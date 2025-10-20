using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

using Pipelane.Application.Ai;
using Pipelane.Application.Storage;
using Pipelane.Domain.Enums;
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

    public AiController(
        ITextAiService aiService,
        ITenantProvider tenantProvider,
        IAppDbContext db,
        ILogger<AiController> logger)
    {
        _aiService = aiService;
        _tenantProvider = tenantProvider;
        _db = db;
        _logger = logger;
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

            return Ok(new SuggestFollowupResponse(
                result.ScheduledAtUtc.ToString("o"),
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

    private async Task<bool> IsOptOutAsync(Channel channel, Guid? contactId, string? contextSnippet, CancellationToken ct)
    {
        if (channel is not (Channel.Email or Channel.Sms))
        {
            return false;
        }

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

        var prospect = await _db.Prospects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == contactId.Value, ct)
            .ConfigureAwait(false);

        return prospect?.OptedOut == true;
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
        [property: JsonPropertyName("scheduledAtIso")] string ScheduledAtIso,
        [property: JsonPropertyName("angle")] string Angle,
        [property: JsonPropertyName("previewText")] string PreviewText);
}

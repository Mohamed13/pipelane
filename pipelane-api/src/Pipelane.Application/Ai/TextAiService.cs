using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Pipelane.Application.Ai.Prompts;
using Pipelane.Domain.Enums;

namespace Pipelane.Application.Ai;

public interface ITextAiService
{
    Task<GenerateMessageResult> GenerateMessageAsync(Guid tenantId, GenerateMessageCommand command, CancellationToken ct);
    Task<ClassifyReplyResult> ClassifyReplyAsync(Guid tenantId, ClassifyReplyCommand command, CancellationToken ct);
    Task<SuggestFollowupResult> SuggestFollowupAsync(Guid tenantId, SuggestFollowupCommand command, CancellationToken ct);
}

public sealed class TextAiOptions
{
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "gpt-4o-mini";
    public string BaseUrl { get; set; } = "https://api.openai.com/";
    public decimal DailyBudgetEur { get; set; } = 5m;
    public decimal PromptTokenPriceEurPerThousand { get; set; } = 0.0018m;
    public decimal CompletionTokenPriceEurPerThousand { get; set; } = 0.005m;
}

public sealed class AiDisabledException : InvalidOperationException
{
    public AiDisabledException() : base("AI integration is disabled. Provide OPENAI_API_KEY.") { }
}

public sealed class AiBudgetExceededException : InvalidOperationException
{
    public AiBudgetExceededException(decimal budget, decimal attempted)
        : base($"AI daily budget exceeded ({attempted:0.00}€ over budget {budget:0.00}€).") { }
}

internal sealed record AiCompletionResponse(string Content, int? PromptTokens, int? CompletionTokens);

public sealed class TextAiService : ITextAiService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TextAiOptions _options;
    private readonly ILogger<TextAiService> _logger;
    private readonly IMemoryCache _cache;
    private readonly TimeProvider _clock;

    private static readonly ConcurrentDictionary<string, object> BudgetLocks = new();

    public TextAiService(
        IHttpClientFactory httpClientFactory,
        IOptions<TextAiOptions> options,
        ILogger<TextAiService> logger,
        IMemoryCache cache,
        TimeProvider? clock = null)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
        _cache = cache;
        _clock = clock ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public async Task<GenerateMessageResult> GenerateMessageAsync(Guid tenantId, GenerateMessageCommand command, CancellationToken ct)
    {
        ValidateGenerate(command);
        var fallback = BuildFallbackMessage(command);

        var apiKey = TryGetApiKey();
        if (apiKey is null)
        {
            _logger.LogDebug("AI disabled; returning heuristic message for tenant {TenantId}", tenantId);
            return fallback;
        }

        var prompts = BuildGeneratePrompts(command);
        var completion = await CallChatCompletionAsync(apiKey, tenantId, "generate-message", prompts.System, prompts.User, ct);
        if (completion is null)
        {
            return fallback;
        }

        try
        {
            var parsed = ParseGenerate(completion.Content, command, fallback);
            TrackUsage(tenantId, completion.PromptTokens, completion.CompletionTokens);
            return parsed with { Source = AiContentSource.OpenAi };
        }
        catch (AiBudgetExceededException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falling back to heuristic generation for tenant {TenantId}", tenantId);
            return fallback;
        }
    }

    /// <inheritdoc/>
    public async Task<ClassifyReplyResult> ClassifyReplyAsync(Guid tenantId, ClassifyReplyCommand command, CancellationToken ct)
    {
        ValidateClassify(command);
        var fallback = BuildFallbackClassification(command);
        var apiKey = TryGetApiKey();
        if (apiKey is null)
        {
            _logger.LogDebug("AI disabled; returning heuristic classification for tenant {TenantId}", tenantId);
            return fallback;
        }

        var prompts = BuildClassifyPrompts(command);
        var completion = await CallChatCompletionAsync(apiKey, tenantId, "classify-reply", prompts.System, prompts.User, ct);
        if (completion is null)
        {
            return fallback;
        }

        try
        {
            var parsed = ParseClassify(completion.Content);
            TrackUsage(tenantId, completion.PromptTokens, completion.CompletionTokens);
            return parsed with { Source = AiContentSource.OpenAi };
        }
        catch (AiBudgetExceededException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falling back to heuristic classification for tenant {TenantId}", tenantId);
            return fallback;
        }
    }

    /// <inheritdoc/>
    public async Task<SuggestFollowupResult> SuggestFollowupAsync(Guid tenantId, SuggestFollowupCommand command, CancellationToken ct)
    {
        ValidateFollowup(command);
        var fallback = BuildFallbackFollowup(command);

        var apiKey = TryGetApiKey();
        if (apiKey is null)
        {
            _logger.LogDebug("AI disabled; returning heuristic follow-up for tenant {TenantId}", tenantId);
            return fallback;
        }

        var prompts = BuildFollowupPrompts(command);
        var completion = await CallChatCompletionAsync(apiKey, tenantId, "suggest-followup", prompts.System, prompts.User, ct);
        if (completion is null)
        {
            return fallback;
        }

        try
        {
            var parsed = ParseFollowup(completion.Content, command, fallback);
            TrackUsage(tenantId, completion.PromptTokens, completion.CompletionTokens);
            return parsed with { Source = AiContentSource.OpenAi };
        }
        catch (AiBudgetExceededException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falling back to heuristic followup for tenant {TenantId}", tenantId);
            return fallback;
        }
    }

    private string? TryGetApiKey() => string.IsNullOrWhiteSpace(_options.ApiKey) ? null : _options.ApiKey;

    private void ValidateGenerate(GenerateMessageCommand command)
    {
        if (command.Context is null)
        {
            throw new ArgumentException("Context is required.", nameof(command));
        }
        if (string.IsNullOrWhiteSpace(command.Context.Pitch))
        {
            throw new ArgumentException("Pitch must be provided.", nameof(command));
        }
    }

    private static void ValidateClassify(ClassifyReplyCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Text))
        {
            throw new ArgumentException("Text must be provided.", nameof(command));
        }
    }

    private static void ValidateFollowup(SuggestFollowupCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Timezone))
        {
            throw new ArgumentException("Timezone must be provided.", nameof(command));
        }
    }

    private PromptPair BuildGeneratePrompts(GenerateMessageCommand command)
    {
        var ctx = command.Context;
        var replacements = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["language"] = command.Language ?? "en",
            ["company"] = string.IsNullOrWhiteSpace(ctx.Company) ? "non renseignée" : ctx.Company,
            ["role"] = string.IsNullOrWhiteSpace(ctx.Role) ? "non renseigné" : ctx.Role,
            ["painPoints"] = ctx.PainPoints is { Count: > 0 } ? string.Join(", ", ctx.PainPoints) : "non renseignées",
            ["pitch"] = ctx.Pitch,
            ["calendlyUrl"] = string.IsNullOrWhiteSpace(ctx.CalendlyUrl) ? "(non fourni)" : ctx.CalendlyUrl,
            ["lastMessageSnippet"] = string.IsNullOrWhiteSpace(ctx.LastMessageSnippet) ? "(aucun historique récent)" : ctx.LastMessageSnippet,
            ["channel"] = command.Channel.ToString().ToLowerInvariant()
        };

        var system = RenderTemplate(AiPromptCatalog.GenerateMessageSystem, replacements);
        var user = RenderTemplate(AiPromptCatalog.GenerateMessageUser, replacements);
        return new PromptPair(system, user);
    }

    private PromptPair BuildClassifyPrompts(ClassifyReplyCommand command)
    {
        var replacements = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["text"] = command.Text,
            ["language"] = command.Language ?? "unknown"
        };

        var system = RenderTemplate(AiPromptCatalog.ClassifyReplySystem, replacements);
        var user = RenderTemplate(AiPromptCatalog.ClassifyReplyUser, replacements);
        return new PromptPair(system, user);
    }

    private PromptPair BuildFollowupPrompts(SuggestFollowupCommand command)
    {
        var hints = command.PerformanceHints;
        var perfText = hints is null
            ? "(aucun)"
            : $"goodHours: [{string.Join(", ", hints.GoodHours ?? Array.Empty<int>())}], badDays: [{string.Join(", ", hints.BadDays ?? Array.Empty<string>())}]";

        var replacements = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["lastInteractionAt"] = command.LastInteractionAt.ToString("o"),
            ["read"] = command.Read ? "true" : "false",
            ["timezone"] = command.Timezone,
            ["historySnippet"] = string.IsNullOrWhiteSpace(command.HistorySnippet) ? "(aucun)" : command.HistorySnippet,
            ["performanceHints"] = perfText,
            ["channel"] = command.Channel.ToString().ToLowerInvariant(),
            ["language"] = command.Language ?? "unknown"
        };

        var system = RenderTemplate(AiPromptCatalog.SuggestFollowupSystem, replacements);
        var user = RenderTemplate(AiPromptCatalog.SuggestFollowupUser, replacements);
        return new PromptPair(system, user);
    }

    private async Task<AiCompletionResponse?> CallChatCompletionAsync(string apiKey, Guid tenantId, string operation, string systemPrompt, string userPrompt, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("OpenAI");
        if (!client.DefaultRequestHeaders.Contains("Authorization"))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        var payload = JsonSerializer.Serialize(new
        {
            model = _options.Model,
            temperature = 0.4,
            messages = new[]
            {
                new { role = "system", content = ComposeSystemPrompt(systemPrompt) },
                new { role = "user", content = userPrompt }
            }
        });

        AiCompletionResponse? completion = null;
        Exception? last = null;

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };

            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(20));

                var sw = Stopwatch.StartNew();
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token).ConfigureAwait(false);
                sw.Stop();
                response.EnsureSuccessStatusCode();

                await using var stream = await response.Content.ReadAsStreamAsync(timeoutCts.Token).ConfigureAwait(false);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: timeoutCts.Token).ConfigureAwait(false);
                var root = document.RootElement;
                var content = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
                int? promptTokens = null;
                int? completionTokens = null;
                if (root.TryGetProperty("usage", out var usage))
                {
                    if (usage.TryGetProperty("prompt_tokens", out var pt))
                    {
                        promptTokens = pt.GetInt32();
                    }
                    if (usage.TryGetProperty("completion_tokens", out var ctokens))
                    {
                        completionTokens = ctokens.GetInt32();
                    }
                }

                _logger.LogInformation("AI request {Operation} for tenant {TenantId} succeeded in {Elapsed} ms (prompt {PromptTokens}, completion {CompletionTokens})",
                    operation, tenantId, sw.ElapsedMilliseconds, promptTokens, completionTokens);

                completion = new AiCompletionResponse(content, promptTokens, completionTokens);
                last = null;
                break;
            }
            catch (OperationCanceledException oce) when (!ct.IsCancellationRequested)
            {
                last = oce;
            }
            catch (HttpRequestException hre)
            {
                last = hre;
            }
        }

        if (completion is null && last is not null)
        {
            _logger.LogWarning(last, "OpenAI call {Operation} failed for tenant {TenantId}", operation, tenantId);
        }

        return completion;
    }

    private static string ComposeSystemPrompt(string systemPrompt)
    {
        var trimmed = systemPrompt.Trim();
        if (!trimmed.EndsWith(".", StringComparison.Ordinal))
        {
            trimmed += ".";
        }
        return $"{trimmed}\nRéponds uniquement au format demandé. Aucune explication additionnelle.";
    }

    private static string RenderTemplate(string template, IDictionary<string, string?> values)
    {
        var result = template;
        foreach (var kvp in values)
        {
            result = result.Replace("{{" + kvp.Key + "}}", kvp.Value ?? string.Empty, StringComparison.Ordinal);
        }
        return result;
    }

    private readonly record struct PromptPair(string System, string User);

    private void TrackUsage(Guid tenantId, int? promptTokens, int? completionTokens)
    {
        var cost = EstimateCost(promptTokens, completionTokens);
        if (cost <= 0 || _options.DailyBudgetEur <= 0)
        {
            return;
        }

        var today = _clock.GetUtcNow().UtcDateTime.Date;
        var key = $"ai-budget:{tenantId}:{today:yyyyMMdd}";
        var sync = BudgetLocks.GetOrAdd(key, _ => new object());

        lock (sync)
        {
            var current = _cache.TryGetValue(key, out decimal existing) ? existing : 0m;
            var attempted = current + cost;
            if (attempted > _options.DailyBudgetEur)
            {
                throw new AiBudgetExceededException(_options.DailyBudgetEur, attempted);
            }

            _cache.Set(key, attempted, TimeSpan.FromDays(2));
        }
    }

    private decimal EstimateCost(int? promptTokens, int? completionTokens)
    {
        var promptCost = promptTokens.HasValue
            ? (promptTokens.Value / 1000m) * _options.PromptTokenPriceEurPerThousand
            : 0m;
        var completionCost = completionTokens.HasValue
            ? (completionTokens.Value / 1000m) * _options.CompletionTokenPriceEurPerThousand
            : 0m;

        return decimal.Round(promptCost + completionCost, 4, MidpointRounding.AwayFromZero);
    }

    private static GenerateMessageResult BuildFallbackMessage(GenerateMessageCommand command)
    {
        var ctx = command.Context;
        var recipient = string.Join(" ", new[] { ctx.FirstName, ctx.LastName }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
        if (string.IsNullOrWhiteSpace(recipient))
        {
            recipient = ctx.Company ?? "there";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Hi {recipient},");
        sb.AppendLine();
        if (ctx.PainPoints is { Count: > 0 })
        {
            sb.AppendLine($"I often hear leaders struggling with {string.Join(", ", ctx.PainPoints)}.");
        }
        sb.AppendLine(ctx.Pitch);
        if (!string.IsNullOrWhiteSpace(ctx.CalendlyUrl))
        {
            sb.AppendLine();
            sb.AppendLine($"If it helps, here is a quick link to book time: {ctx.CalendlyUrl}");
        }
        sb.AppendLine();
        sb.AppendLine("Best regards,");
        sb.AppendLine("The Pipelane team");

        var text = LimitWords(sb.ToString(), 120);
        var html = $"<p>{text.Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n\n", "</p><p>", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal)}</p>";
        if (command.Channel == Channel.Email)
        {
            html += "<p style=\"font-size:12px;color:#666\">To opt out, reply STOP at any time.</p>";
        }

        return new GenerateMessageResult(
            Subject: $"Quick idea for {ctx.Company ?? "your team"}",
            Text: text,
            Html: html,
            LanguageDetected: command.Language ?? "en",
            Source: AiContentSource.Fallback);
    }

    private static ClassifyReplyResult BuildFallbackClassification(ClassifyReplyCommand command)
    {
        var text = command.Text.ToLowerInvariant();
        var (intent, confidence) = text switch
        {
            var s when s.Contains("interested") || s.Contains("great") || s.Contains("let's talk") => (AiReplyIntent.Interested, 0.85),
            var s when s.Contains("maybe") || s.Contains("later") || s.Contains("follow up") => (AiReplyIntent.Maybe, 0.6),
            var s when s.Contains("not now") || s.Contains("busy") => (AiReplyIntent.NotNow, 0.55),
            var s when s.Contains("unsubscribe") || s.Contains("stop") || s.Contains("remove me") => (AiReplyIntent.NotRelevant, 0.9),
            var s when s.Contains("out of office") || s.Contains("ooo") || s.Contains("vacation") => (AiReplyIntent.Ooo, 0.8),
            var s when s.Contains("auto") && s.Contains("reply") => (AiReplyIntent.AutoReply, 0.75),
            _ => (AiReplyIntent.Maybe, 0.5)
        };

        return new ClassifyReplyResult(intent, confidence, AiContentSource.Fallback);
    }

    private static SuggestFollowupResult BuildFallbackFollowup(SuggestFollowupCommand command)
    {
        var scheduledUtc = ComputeSuggestedUtc(command);
        var angle = command.Channel switch
        {
            Channel.Email => AiFollowupAngle.Value,
            Channel.Whatsapp => AiFollowupAngle.Reminder,
            Channel.Sms => AiFollowupAngle.Question,
            _ => AiFollowupAngle.Reminder
        };
        var preview = BuildFollowupPreview(command, scheduledUtc, angle);
        return new SuggestFollowupResult(scheduledUtc, angle, preview, AiContentSource.Fallback);
    }

    private static GenerateMessageResult ParseGenerate(string content, GenerateMessageCommand command, GenerateMessageResult fallback)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            var subject = root.TryGetProperty("subject", out var subjectProp) ? subjectProp.GetString() : fallback.Subject;
            var text = root.TryGetProperty("text", out var textProp) ? textProp.GetString() : fallback.Text;
            var html = root.TryGetProperty("html", out var htmlProp) ? htmlProp.GetString() : fallback.Html;
            var language = root.TryGetProperty("languageDetected", out var languageProp)
                ? languageProp.GetString()
                : fallback.LanguageDetected;

            text = LimitWords(text ?? fallback.Text, 120);
            html = string.IsNullOrWhiteSpace(html)
                ? $"<p>{text.Replace("\n\n", "</p><p>", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal)}</p>"
                : html;

            return fallback with
            {
                Subject = subject ?? fallback.Subject,
                Text = text,
                Html = html,
                LanguageDetected = language ?? command.Language ?? DetectLanguage(text)
            };
        }
        catch (JsonException)
        {
            return fallback;
        }
    }

    private static ClassifyReplyResult ParseClassify(string content)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            var intentRaw = root.TryGetProperty("intent", out var intentProp) ? intentProp.GetString() : null;
            var confidence = root.TryGetProperty("confidence", out var confidenceProp) ? confidenceProp.GetDouble() : 0.5;

            var intent = intentRaw?.Trim().ToLowerInvariant() switch
            {
                "interested" => AiReplyIntent.Interested,
                "maybe" => AiReplyIntent.Maybe,
                "notnow" => AiReplyIntent.NotNow,
                "not_now" => AiReplyIntent.NotNow,
                "notrelevant" => AiReplyIntent.NotRelevant,
                "not relevant" => AiReplyIntent.NotRelevant,
                "ooo" => AiReplyIntent.Ooo,
                "outofoffice" => AiReplyIntent.Ooo,
                "autoreply" or "auto" or "auto_reply" => AiReplyIntent.AutoReply,
                _ => AiReplyIntent.Maybe
            };

            return new ClassifyReplyResult(intent, Math.Clamp(confidence, 0, 1), AiContentSource.OpenAi);
        }
        catch (JsonException)
        {
            return new ClassifyReplyResult(AiReplyIntent.Maybe, 0.5, AiContentSource.Fallback);
        }
    }

    private static SuggestFollowupResult ParseFollowup(string content, SuggestFollowupCommand command, SuggestFollowupResult fallback)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            var iso = root.TryGetProperty("scheduledAtIso", out var schedProp) ? schedProp.GetString() : null;
            var angleRaw = root.TryGetProperty("angle", out var angleProp) ? angleProp.GetString() : null;
            var preview = root.TryGetProperty("previewText", out var previewProp) ? previewProp.GetString() : fallback.PreviewText;

            DateTime scheduledUtc = fallback.ScheduledAtUtc;
            if (!string.IsNullOrWhiteSpace(iso) && DateTimeOffset.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto))
            {
                scheduledUtc = dto.ToUniversalTime().UtcDateTime;
            }

            scheduledUtc = EnsureQuietHoursUtc(scheduledUtc, command);

            var angle = angleRaw?.Trim().ToLowerInvariant() switch
            {
                "value" => AiFollowupAngle.Value,
                "social" => AiFollowupAngle.Social,
                "question" => AiFollowupAngle.Question,
                "reminder" => AiFollowupAngle.Reminder,
                _ => fallback.Angle
            };

            var safePreview = LimitWords(preview ?? fallback.PreviewText, 90);
            return fallback with
            {
                ScheduledAtUtc = scheduledUtc,
                Angle = angle,
                PreviewText = safePreview
            };
        }
        catch (JsonException)
        {
            return fallback;
        }
    }

    private static DateTime ComputeSuggestedUtc(SuggestFollowupCommand command)
    {
        var timezone = ResolveTimezone(command.Timezone);
        var baseUtc = command.LastInteractionAt.Kind switch
        {
            DateTimeKind.Utc => command.LastInteractionAt,
            DateTimeKind.Local => command.LastInteractionAt.ToUniversalTime(),
            _ => DateTime.SpecifyKind(command.LastInteractionAt, DateTimeKind.Utc)
        };

        var baseLocal = TimeZoneInfo.ConvertTimeFromUtc(baseUtc, timezone);
        var delta = command.Read ? TimeSpan.FromHours(48) : TimeSpan.FromHours(24);
        var candidate = baseLocal + delta;

        if (command.PerformanceHints?.GoodHours is { Count: > 0 })
        {
            candidate = new DateTime(candidate.Year, candidate.Month, candidate.Day, command.PerformanceHints.GoodHours[0], 0, 0, candidate.Kind);
        }

        candidate = AdjustLocalQuietHours(candidate);

        if (command.PerformanceHints?.BadDays is { Count: > 0 })
        {
            var banned = command.PerformanceHints.BadDays.Select(d => d[..3].ToLowerInvariant()).ToHashSet();
            while (banned.Contains(candidate.ToString("ddd", CultureInfo.InvariantCulture).ToLowerInvariant()))
            {
                candidate = AdjustLocalQuietHours(candidate.AddDays(1));
            }
        }

        return TimeZoneInfo.ConvertTimeToUtc(candidate, timezone);
    }

    private static DateTime EnsureQuietHoursUtc(DateTime scheduledUtc, SuggestFollowupCommand command)
    {
        var timezone = ResolveTimezone(command.Timezone);
        var local = TimeZoneInfo.ConvertTimeFromUtc(scheduledUtc, timezone);
        var adjusted = AdjustLocalQuietHours(local);
        return TimeZoneInfo.ConvertTimeToUtc(adjusted, timezone);
    }

    private static DateTime AdjustLocalQuietHours(DateTime local)
    {
        var hour = local.Hour;
        if (hour >= 22)
        {
            return new DateTime(local.Year, local.Month, local.Day, 10, 30, 0, local.Kind).AddDays(1);
        }

        if (hour < 8)
        {
            return new DateTime(local.Year, local.Month, local.Day, 10, 30, 0, local.Kind);
        }

        return local.Minute == 30 ? local : new DateTime(local.Year, local.Month, local.Day, local.Hour, 30, 0, local.Kind);
    }

    private static string BuildFollowupPreview(SuggestFollowupCommand command, DateTime scheduledUtc, AiFollowupAngle angle)
    {
        var timezone = ResolveTimezone(command.Timezone);
        var local = TimeZoneInfo.ConvertTimeFromUtc(scheduledUtc, timezone);
        var intro = angle switch
        {
            AiFollowupAngle.Value => "Sharing a quick outcome our clients achieved.",
            AiFollowupAngle.Social => "Spotted a relevant update you might like.",
            AiFollowupAngle.Question => "Checking in with a short question for you.",
            _ => "Gentle reminder in case it dropped off."
        };

        var history = string.IsNullOrWhiteSpace(command.HistorySnippet)
            ? string.Empty
            : $" Context: {LimitWords(command.HistorySnippet, 25)}";

        return $"{intro} Planning to reach out around {local:dddd HH:mm} local time.{history}".Trim();
    }

    private static TimeZoneInfo ResolveTimezone(string timezone)
    {
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

    private static string LimitWords(string text, int maxWords)
    {
        var tokens = text
            .Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length <= maxWords)
        {
            return text.Trim();
        }

        var sb = new StringBuilder();
        for (var i = 0; i < maxWords; i++)
        {
            sb.Append(tokens[i]);
            sb.Append(' ');
        }
        sb.Append("...");
        return sb.ToString().Trim();
    }

    private static string DetectLanguage(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "en";
        }

        var accented = text.Count(c => "éàèùâêîôûç".Contains(char.ToLowerInvariant(c)));
        return accented > 3 ? "fr" : "en";
    }
}

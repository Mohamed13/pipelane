using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Pipelane.Domain.Entities.Prospecting;
using Pipelane.Domain.Enums;
using Pipelane.Domain.Enums.Prospecting;

namespace Pipelane.Application.Prospecting;

public interface IProspectingAiService
{
    Task<(string subject, string html, string? text, int? promptTokens, int? completionTokens, decimal? costUsd)> GenerateEmailAsync(Prospect prospect, ProspectingSequenceStep step, ProspectingCampaign? campaign, CancellationToken ct);
    Task<(ReplyIntent intent, double confidence, string? extractedDatesJson)> ClassifyReplyAsync(ProspectReply reply, CancellationToken ct);
    Task<(string subject, string html, string? text, int? promptTokens, int? completionTokens, decimal? costUsd)> CreateAutoReplyDraftAsync(ProspectReply reply, Prospect prospect, CancellationToken ct);
}

public sealed class ProspectingAiOptions
{
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "gpt-4o-mini";
    public string BaseUrl { get; set; } = "https://api.openai.com/";
    public decimal PricePer1KPromptTokensUsd { get; set; } = 0.005m;
    public decimal PricePer1KCompletionTokensUsd { get; set; } = 0.015m;
}

public sealed class ProspectingAiService : IProspectingAiService
{
    private readonly ProspectingAiOptions _options;
    private readonly ILogger<ProspectingAiService> _logger;

    public ProspectingAiService(IOptions<ProspectingAiOptions> options, ILogger<ProspectingAiService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<(string subject, string html, string? text, int? promptTokens, int? completionTokens, decimal? costUsd)> GenerateEmailAsync(Prospect prospect, ProspectingSequenceStep step, ProspectingCampaign? campaign, CancellationToken ct)
    {
        var (subject, html, text) = ComposeFallbackEmail(prospect, step, campaign);
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogDebug("OpenAI API key missing, returning fallback email for prospect {ProspectId}", prospect.Id);
            return (subject, html, text, null, null, null);
        }

        try
        {
            var prompt = BuildGenerationPrompt(prospect, step, campaign);
            var response = await CallChatCompletionAsync(prompt, ct);
            if (response is null)
            {
                return (subject, html, text, null, null, null);
            }

            var content = response.Value.content;
            if (!string.IsNullOrWhiteSpace(content))
            {
                (subject, html, text) = ParseEmailContent(content, prospect, subject);
            }
            var cost = EstimateCost(response.Value.promptTokens, response.Value.completionTokens);
            return (subject, html, text, response.Value.promptTokens, response.Value.completionTokens, cost);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falling back to heuristic email generation for prospect {ProspectId}", prospect.Id);
            return (subject, html, text, null, null, null);
        }
    }

    /// <inheritdoc/>
    public Task<(ReplyIntent intent, double confidence, string? extractedDatesJson)> ClassifyReplyAsync(ProspectReply reply, CancellationToken ct)
    {
        var text = reply.TextBody ?? reply.HtmlBody ?? string.Empty;
        var lowered = text.ToLowerInvariant();
        var (intent, confidence) = lowered switch
        {
            var s when s.Contains("interested") || s.Contains("let's talk") || s.Contains("book") => (ReplyIntent.Interested, 0.9),
            var s when s.Contains("schedule") || s.Contains("meet") || s.Contains("call") && s.Contains("next") => (ReplyIntent.MeetingRequested, 0.85),
            var s when s.Contains("unsubscribe") || s.Contains("stop") => (ReplyIntent.Unsubscribe, 0.95),
            var s when s.Contains("not interested") || s.Contains("no thanks") => (ReplyIntent.NotInterested, 0.8),
            var s when s.Contains("out of office") || s.Contains("ooo") => (ReplyIntent.OutOfOffice, 0.7),
            var s when s.Contains("support") || s.Contains("help") => (ReplyIntent.Support, 0.6),
            var s when s.Contains("bounce") || s.Contains("undeliverable") => (ReplyIntent.Bounce, 0.9),
            _ => (ReplyIntent.Unknown, 0.4)
        };

        var extractedDate = ExtractDate(text);
        var datesJson = extractedDate.HasValue ? JsonSerializer.Serialize(new[] { extractedDate.Value.ToString("o") }) : null;
        return Task.FromResult((intent, confidence, datesJson));
    }

    /// <inheritdoc/>
    public async Task<(string subject, string html, string? text, int? promptTokens, int? completionTokens, decimal? costUsd)> CreateAutoReplyDraftAsync(ProspectReply reply, Prospect prospect, CancellationToken ct)
    {
        var (subject, html, text) = ComposeAutoReply(reply, prospect);
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return (subject, html, text, null, null, null);
        }

        try
        {
            var prompt = BuildAutoReplyPrompt(reply, prospect);
            var response = await CallChatCompletionAsync(prompt, ct);
            if (response is null)
            {
                return (subject, html, text, null, null, null);
            }

            var content = response.Value.content;
            if (!string.IsNullOrWhiteSpace(content))
            {
                (subject, html, text) = ParseEmailContent(content, prospect, subject);
            }
            var cost = EstimateCost(response.Value.promptTokens, response.Value.completionTokens);
            return (subject, html, text, response.Value.promptTokens, response.Value.completionTokens, cost);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to reach OpenAI for auto-reply draft on reply {ReplyId}", reply.Id);
            return (subject, html, text, null, null, null);
        }
    }

    private async Task<(string content, int? promptTokens, int? completionTokens)?> CallChatCompletionAsync(string prompt, CancellationToken ct)
    {
        using var client = new HttpClient { BaseAddress = new Uri(_options.BaseUrl) };
        var request = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        var payload = new
        {
            model = _options.Model,
            messages = new[]
            {
                new { role = "system", content = "You are a senior SDR assistant creating concise B2B outreach emails. Always return JSON with keys subject,text,html." },
                new { role = "user", content = prompt }
            },
            temperature = 0.65
        };
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("OpenAI API returned {Status}: {Message}", response.StatusCode, await response.Content.ReadAsStringAsync(ct));
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var content = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
        int? promptTokens = null;
        int? completionTokens = null;
        if (root.TryGetProperty("usage", out var usage))
        {
            if (usage.TryGetProperty("prompt_tokens", out var promptEl)) promptTokens = promptEl.GetInt32();
            if (usage.TryGetProperty("completion_tokens", out var completionEl)) completionTokens = completionEl.GetInt32();
        }

        return (content, promptTokens, completionTokens);
    }

    private static (string subject, string html, string? text) ComposeFallbackEmail(Prospect prospect, ProspectingSequenceStep step, ProspectingCampaign? campaign)
    {
        var name = !string.IsNullOrWhiteSpace(prospect.FirstName) ? prospect.FirstName : prospect.Company ?? "there";
        var subject = $"Idea to boost {prospect.Company ?? "your team"}'s pipeline";
        var builder = new StringBuilder();
        builder.AppendLine($"Hi {name},");
        builder.AppendLine();
        builder.AppendLine("I noticed you're driving growth and wanted to share a quick idea on how we help similar teams automate their outbound while keeping messages highly personalised.");
        builder.AppendLine();
        builder.AppendLine("Would you be open to a short call next week to see if this could match your current priorities?");
        builder.AppendLine();
        builder.AppendLine("Best regards,");
        builder.AppendLine("Pipelane Team");

        var html = @$"<p>Hi {name},</p>
<p>I noticed you're driving growth and wanted to share a quick idea on how similar teams automate outbound while staying personal.</p>
<p>Would you be open to a short call next week to see if this could align with your priorities?</p>
<p>Best regards,<br/>Pipelane Team</p>
<p style=""font-size:12px;color:#666"">You received this email because your address is publicly listed for {prospect.Company ?? "your company"}. <a href=""{{optOutUrl}}"">Unsubscribe</a>.</p>";
        var text = builder.ToString();
        return (subject, html, text);
    }

    private static (string subject, string html, string? text) ComposeAutoReply(ProspectReply reply, Prospect prospect)
    {
        var subject = reply.Subject != null && reply.Subject.StartsWith("Re:", StringComparison.OrdinalIgnoreCase)
            ? reply.Subject
            : $"Re: {reply.Subject ?? "our conversation"}";
        var name = !string.IsNullOrWhiteSpace(prospect.FirstName) ? prospect.FirstName : prospect.Company ?? "there";
        var text = $"Hi {name},\n\nThanks for your reply! Happy to lock a time that works for you. Here is my calendar: {{calendlyLink}}.\n\nLooking forward to it.\n\nBest regards,\nPipelane Team";
        var html = @$"<p>Hi {name},</p>
<p>Thanks for your reply! I'd love to continue the conversation. You can pick a slot that works for you here: <a href=""{{calendlyLink}}"">Book a time</a>.</p>
<p>Looking forward to connecting.<br/>Pipelane Team</p>";
        return (subject, html, text);
    }

    private static string BuildGenerationPrompt(Prospect prospect, ProspectingSequenceStep step, ProspectingCampaign? campaign)
    {
        var persona = prospect.Persona ?? step.MetadataJson ?? campaign?.SettingsJson;
        var goal = campaign?.Name ?? "prospecting outreach";
        var prompt = $"""
Generate a JSON object with keys subject, text, html for an outreach email.
Prospect:
- Name: {prospect.FirstName} {prospect.LastName}
- Company: {prospect.Company}
- Title: {prospect.Title}
- Persona: {persona}
- Recent intent: {prospect.Source}

Step type: {step.StepType} on day {step.OffsetDays}.
Goal: {goal}.
Constraints: keep under 120 words, add clear CTA, respectful tone, include opt-out footer.
HTML should include paragraphs and bold CTA.
""";
        return prompt;
    }

    private static string BuildAutoReplyPrompt(ProspectReply reply, Prospect prospect)
    {
        var original = reply.TextBody ?? reply.HtmlBody ?? string.Empty;
        return string.Join(Environment.NewLine, new[]
        {
            "Create a JSON object with subject,text,html for a polite follow-up responding to the message below.",
            $"Prospect name: {prospect.FirstName} {prospect.LastName}",
            $"Prospect company: {prospect.Company}",
            "They wrote:",
            $"\"\"\"{original}\"\"\"",
            "Respond with gratitude, propose a Calendly link placeholder {{calendlyLink}}, keep under 80 words."
        });
    }

    private static (string subject, string html, string? text) ParseEmailContent(string content, Prospect prospect, string fallbackSubject)
    {
        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            var subject = root.TryGetProperty("subject", out var subjectProp) ? subjectProp.GetString() ?? fallbackSubject : fallbackSubject;
            var text = root.TryGetProperty("text", out var textProp) ? textProp.GetString() : null;
            var html = root.TryGetProperty("html", out var htmlProp) ? htmlProp.GetString() ?? text : text;
            if (string.IsNullOrWhiteSpace(html) && !string.IsNullOrWhiteSpace(text))
            {
                html = $"<p>{text.Replace("\n", "<br/>", StringComparison.Ordinal)}</p>";
            }
            if (html != null && !html.Contains("unsubscribe", StringComparison.OrdinalIgnoreCase))
            {
                html += @$"<p style=""font-size:12px;color:#666"">You received this email because your address is listed for {prospect.Company ?? "your company"}. <a href=""{{optOutUrl}}"">Unsubscribe</a>.</p>";
            }
            return (subject, html ?? string.Empty, text);
        }
        catch (JsonException)
        {
            return ComposeFallbackEmail(prospect, new ProspectingSequenceStep { StepType = SequenceStepType.Email, Channel = Channel.Email, OffsetDays = 0 }, null);
        }
    }

    private decimal? EstimateCost(int? promptTokens, int? completionTokens)
    {
        if (promptTokens is null && completionTokens is null)
        {
            return null;
        }

        var promptCost = promptTokens.HasValue ? (promptTokens.Value / 1000m) * _options.PricePer1KPromptTokensUsd : 0m;
        var completionCost = completionTokens.HasValue ? (completionTokens.Value / 1000m) * _options.PricePer1KCompletionTokensUsd : 0m;
        return decimal.Round(promptCost + completionCost, 6, MidpointRounding.AwayFromZero);
    }

    private static DateTime? ExtractDate(string text)
    {
        foreach (var token in text.Split(new[] { ' ', '\n', '\r', ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (DateTime.TryParse(token, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            {
                return parsed.ToUniversalTime();
            }
        }
        return null;
    }
}






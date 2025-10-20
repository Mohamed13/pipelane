using System;
using System.Collections.Generic;

using Pipelane.Domain.Enums;

namespace Pipelane.Application.Ai;

public record AiMessageContext(
    string? FirstName,
    string? LastName,
    string? Company,
    string? Role,
    IReadOnlyList<string>? PainPoints,
    string Pitch,
    string? CalendlyUrl,
    string? LastMessageSnippet);

public record GenerateMessageCommand(
    Guid? ContactId,
    Channel Channel,
    string? Language,
    AiMessageContext Context);

public record GenerateMessageResult(
    string? Subject,
    string Text,
    string? Html,
    string LanguageDetected,
    AiContentSource Source);

public enum AiContentSource
{
    OpenAi,
    Fallback
}

public record ClassifyReplyCommand(
    string Text,
    string? Language);

public record ClassifyReplyResult(
    AiReplyIntent Intent,
    double Confidence,
    AiContentSource Source);

public enum AiReplyIntent
{
    Interested,
    Maybe,
    NotNow,
    NotRelevant,
    Ooo,
    AutoReply
}

public record SuggestFollowupCommand(
    Channel Channel,
    string Timezone,
    DateTime LastInteractionAt,
    bool Read,
    string? Language,
    string? HistorySnippet,
    AiPerformanceHints? PerformanceHints);

public record AiPerformanceHints(
    IReadOnlyList<int>? GoodHours,
    IReadOnlyList<string>? BadDays);

public record SuggestFollowupResult(
    DateTime ScheduledAtUtc,
    AiFollowupAngle Angle,
    string PreviewText,
    AiContentSource Source);

public enum AiFollowupAngle
{
    Reminder,
    Value,
    Social,
    Question
}

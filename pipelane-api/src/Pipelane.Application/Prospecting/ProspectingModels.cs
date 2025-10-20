using System.Collections.Generic;
using System.Linq;

using Pipelane.Domain.Entities.Prospecting;
using Pipelane.Domain.Enums;
using Pipelane.Domain.Enums.Prospecting;

namespace Pipelane.Application.Prospecting;

public record ProspectImportRequest(string Kind, string PayloadBase64, IDictionary<string, string>? FieldMap, bool OverwriteExisting = false);

public record ProspectImportResult(int Imported, int Skipped, int Updated);

public record ProspectingSequenceStepInput(
    SequenceStepType StepType,
    Channel Channel,
    int OffsetDays,
    TimeSpan? SendWindowStartUtc,
    TimeSpan? SendWindowEndUtc,
    string? PromptTemplate,
    string? SubjectTemplate,
    string? GuardrailInstructions,
    bool RequiresApproval,
    string? MetadataJson);

public record ProspectingSequenceCreateRequest(
    string Name,
    string? Description,
    bool IsActive,
    string? TargetPersona,
    string? EntryCriteriaJson,
    IList<ProspectingSequenceStepInput> Steps);

public record ProspectingSequenceUpdateRequest(
    string Name,
    string? Description,
    bool IsActive,
    string? TargetPersona,
    string? EntryCriteriaJson,
    IList<ProspectingSequenceStepInput> Steps);

public record ProspectingSequenceStepDto(
    Guid Id,
    int Order,
    SequenceStepType StepType,
    Channel Channel,
    int OffsetDays,
    TimeSpan? SendWindowStartUtc,
    TimeSpan? SendWindowEndUtc,
    string? PromptTemplate,
    string? SubjectTemplate,
    string? GuardrailInstructions,
    bool RequiresApproval,
    string? MetadataJson);

public record ProspectingSequenceDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    string? TargetPersona,
    string? EntryCriteriaJson,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyList<ProspectingSequenceStepDto> Steps);

public record ProspectDto(
    Guid Id,
    string Email,
    string? FirstName,
    string? LastName,
    string? Company,
    string? Title,
    ProspectStatus Status,
    bool OptedOut,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? LastContactedAtUtc,
    DateTime? LastRepliedAtUtc,
    Guid? SequenceId,
    Guid? CampaignId);

public record ProspectingCampaignCreateRequest(
    string Name,
    Guid SequenceId,
    string SegmentJson,
    string? SettingsJson,
    DateTime? ScheduledAtUtc,
    Guid? OwnerUserId);

public record ProspectingCampaignDto(
    Guid Id,
    string Name,
    Guid SequenceId,
    ProspectingCampaignStatus Status,
    string SegmentJson,
    string? SettingsJson,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? ScheduledAtUtc,
    DateTime? StartedAtUtc,
    DateTime? PausedAtUtc,
    DateTime? CompletedAtUtc);

public record ProspectingCampaignPreview(Guid CampaignId, int Prospects, IList<ProspectingSchedulePreviewStep> Steps);

public record ProspectingSchedulePreviewStep(Guid StepId, DateTime ScheduledAtUtc, string Label);

public record ProspectingAnalyticsResponse(
    int TotalProspects,
    int ActiveCampaigns,
    int EmailsSent,
    int EmailsOpened,
    int RepliesReceived,
    int MeetingsBooked,
    IReadOnlyList<ProspectingSeriesPoint> DailySeries,
    IReadOnlyList<ProspectingStepSeriesPoint> StepBreakdown);

public record ProspectingSeriesPoint(DateTime Date, int Sent, int Opened, int Replies, int Booked);

public record ProspectingStepSeriesPoint(Guid StepId, string Label, int Sent, int Replies, int Meetings);

public record GenerateEmailRequest(Guid ProspectId, Guid StepId, Guid? CampaignId = null, string Variant = "A");

public record GenerateEmailResponse(Guid GenerationId, string Subject, string HtmlBody, string? TextBody, string Variant, int? PromptTokens, int? CompletionTokens, decimal? CostUsd);

public record ClassifyReplyRequest(Guid ReplyId);

public record ClassifyReplyResponse(Guid ReplyId, ReplyIntent Intent, double Confidence, string? ExtractedDatesJson);

public record AutoReplyRequest(Guid ReplyId, Guid? CampaignId = null);

public record AutoReplyResponse(Guid GenerationId, string Subject, string HtmlBody, string? TextBody, string Variant);

public record ProspectReplyDto(
    Guid Id,
    Guid ProspectId,
    string ProspectEmail,
    string? ProspectName,
    ReplyIntent Intent,
    double? Confidence,
    DateTime ReceivedAtUtc,
    string? Subject,
    string? TextBody,
    string? HtmlBody,
    Guid? CampaignId,
    Guid? SendLogId,
    DateTime? ProcessedAtUtc);

public static class ProspectingMappings
{
    public static ProspectingSequenceDto ToDto(this ProspectingSequence sequence) =>
        new(
            sequence.Id,
            sequence.Name,
            sequence.Description,
            sequence.IsActive,
            sequence.TargetPersona,
            sequence.EntryCriteriaJson,
            sequence.CreatedAtUtc,
            sequence.UpdatedAtUtc,
            sequence.Steps
                .OrderBy(s => s.Order)
                .Select(s => new ProspectingSequenceStepDto(
                    s.Id,
                    s.Order,
                    s.StepType,
                    s.Channel,
                    s.OffsetDays,
                    s.SendWindowStartUtc,
                    s.SendWindowEndUtc,
                    s.PromptTemplate,
                    s.SubjectTemplate,
                    s.GuardrailInstructions,
                    s.RequiresApproval,
                    s.MetadataJson))
                .ToList());

    public static ProspectingCampaignDto ToDto(this ProspectingCampaign campaign) =>
        new(
            campaign.Id,
            campaign.Name,
            campaign.SequenceId,
            campaign.Status,
            campaign.SegmentJson,
            campaign.SettingsJson,
            campaign.CreatedAtUtc,
            campaign.UpdatedAtUtc,
            campaign.ScheduledAtUtc,
            campaign.StartedAtUtc,
            campaign.PausedAtUtc,
            campaign.CompletedAtUtc);

    public static ProspectDto ToDto(this Prospect prospect) =>
        new(
            prospect.Id,
            prospect.Email,
            prospect.FirstName,
            prospect.LastName,
            prospect.Company,
            prospect.Title,
            prospect.Status,
            prospect.OptedOut,
            prospect.CreatedAtUtc,
            prospect.UpdatedAtUtc,
            prospect.LastContactedAtUtc,
            prospect.LastRepliedAtUtc,
            prospect.SequenceId,
            prospect.CampaignId);

    public static ProspectReplyDto ToDto(this ProspectReply reply) =>
        new(
            reply.Id,
            reply.ProspectId,
            reply.Prospect?.Email ?? string.Empty,
            string.Join(' ', new[] { reply.Prospect?.FirstName, reply.Prospect?.LastName }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim(),
            reply.Intent,
            reply.IntentConfidence,
            reply.ReceivedAtUtc,
            reply.Subject,
            reply.TextBody,
            reply.HtmlBody,
            reply.CampaignId,
            reply.SendLogId,
            reply.ProcessedAtUtc);
}


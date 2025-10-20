using Pipelane.Domain.Enums.Prospecting;

namespace Pipelane.Domain.Entities.Prospecting;

public class ProspectReply : BaseEntity
{
    public Guid ProspectId { get; set; }
    public Guid? CampaignId { get; set; }
    public Guid? SendLogId { get; set; }
    public Guid? StepId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string? ProviderMessageId { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
    public string? Subject { get; set; }
    public string? TextBody { get; set; }
    public string? HtmlBody { get; set; }
    public ReplyIntent Intent { get; set; } = ReplyIntent.Unknown;
    public double? IntentConfidence { get; set; }
    public string? ExtractedDatesJson { get; set; }
    public bool AutoReplySuggested { get; set; }
    public Guid? AutoReplyGenerationId { get; set; }
    public Guid? AutoReplySendLogId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
    public string? MetadataJson { get; set; }

    public Prospect? Prospect { get; set; }
    public ProspectingCampaign? Campaign { get; set; }
    public SendLog? SendLog { get; set; }
    public ProspectingSequenceStep? Step { get; set; }
    public EmailGeneration? AutoReplyGeneration { get; set; }
}

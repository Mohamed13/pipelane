using System.Collections.Generic;

using Pipelane.Domain.Enums.Prospecting;

namespace Pipelane.Domain.Entities.Prospecting;

public class SendLog : BaseEntity
{
    public Guid ProspectId { get; set; }
    public Guid? CampaignId { get; set; }
    public Guid? StepId { get; set; }
    public Guid? GenerationId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string? ProviderMessageId { get; set; }
    public SendLogStatus Status { get; set; } = SendLogStatus.Scheduled;
    public DateTime ScheduledAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public DateTime? DeliveredAtUtc { get; set; }
    public DateTime? OpenedAtUtc { get; set; }
    public DateTime? ClickedAtUtc { get; set; }
    public DateTime? BouncedAtUtc { get; set; }
    public DateTime? ComplainedAtUtc { get; set; }
    public DateTime? DeferredUntilUtc { get; set; }
    public int RetryCount { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorReason { get; set; }
    public string? RawPayloadJson { get; set; }
    public string? MetadataJson { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public Prospect? Prospect { get; set; }
    public ProspectingCampaign? Campaign { get; set; }
    public ProspectingSequenceStep? Step { get; set; }
    public EmailGeneration? Generation { get; set; }
    public ICollection<ProspectReply> Replies { get; set; } = new List<ProspectReply>();
}

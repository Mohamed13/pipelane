using System.Collections.Generic;

using Pipelane.Domain.Enums.Prospecting;

namespace Pipelane.Domain.Entities.Prospecting;

public class ProspectingCampaign : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public Guid SequenceId { get; set; }
    public ProspectingCampaignStatus Status { get; set; } = ProspectingCampaignStatus.Draft;
    public Guid? OwnerUserId { get; set; }
    public string SegmentJson { get; set; } = "{}";
    public string? SettingsJson { get; set; }
    public string? StatsJson { get; set; }
    public DateTime? ScheduledAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? PausedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public ProspectingSequence? Sequence { get; set; }
    public ICollection<Prospect> Prospects { get; set; } = new List<Prospect>();
    public ICollection<SendLog> Sends { get; set; } = new List<SendLog>();
    public ICollection<EmailGeneration> Generations { get; set; } = new List<EmailGeneration>();
    public ICollection<ProspectReply> Replies { get; set; } = new List<ProspectReply>();
}

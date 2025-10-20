using System.Collections.Generic;

namespace Pipelane.Domain.Entities.Prospecting;

public class ProspectingSequence : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public string? TargetPersona { get; set; }
    public string? EntryCriteriaJson { get; set; }
    public Guid? OwnerUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public ICollection<ProspectingSequenceStep> Steps { get; set; } = new List<ProspectingSequenceStep>();
    public ICollection<ProspectingCampaign> Campaigns { get; set; } = new List<ProspectingCampaign>();
    public ICollection<Prospect> Prospects { get; set; } = new List<Prospect>();
}

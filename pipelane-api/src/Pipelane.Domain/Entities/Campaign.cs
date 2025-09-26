using Pipelane.Domain.Enums;

namespace Pipelane.Domain.Entities;

public class Campaign : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public Channel PrimaryChannel { get; set; }
    public string? FallbackOrderJson { get; set; }
    public Guid TemplateId { get; set; }
    public string SegmentJson { get; set; } = "{}";
    public DateTime? ScheduledAtUtc { get; set; }
    public CampaignStatus Status { get; set; } = CampaignStatus.Pending;
    public DateTime CreatedAt { get; set; }
}


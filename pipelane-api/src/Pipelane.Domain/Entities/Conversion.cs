namespace Pipelane.Domain.Entities;

public class Conversion : BaseEntity
{
    public Guid ContactId { get; set; }
    public Guid? CampaignId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string? OrderId { get; set; }
    public DateTime RevenueAtUtc { get; set; }
}


namespace Pipelane.Domain.Entities;

public class LeadScore : BaseEntity
{
    public Guid ContactId { get; set; }
    public int Score { get; set; }
    public string? ReasonsJson { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}


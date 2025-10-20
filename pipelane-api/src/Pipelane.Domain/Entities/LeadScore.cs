namespace Pipelane.Domain.Entities;

public class LeadScore : BaseEntity
{
    public Guid? ContactId { get; set; }
    public Guid? ProspectId { get; set; }
    public string Scope { get; set; } = "contact";
    public int Score { get; set; }
    public string? ReasonsJson { get; set; }
    public string? Model { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

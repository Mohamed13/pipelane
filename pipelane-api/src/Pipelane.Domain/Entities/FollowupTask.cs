namespace Pipelane.Domain.Entities;

public class FollowupTask : BaseEntity
{
    public Guid ContactId { get; set; }
    public Guid? MessageId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime DueAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public bool Completed { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? Notes { get; set; }
}

using Pipelane.Domain.Enums;

namespace Pipelane.Domain.Entities;

public enum OutboxStatus { Queued, Sending, Done, Failed }

public class OutboxMessage : BaseEntity
{
    public Guid ContactId { get; set; }
    public Guid? ConversationId { get; set; }
    public Channel Channel { get; set; }
    public MessageType Type { get; set; }
    public Guid? TemplateId { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public string? MetaJson { get; set; }
    public DateTime? ScheduledAtUtc { get; set; }
    public int Attempts { get; set; }
    public int MaxAttempts { get; set; } = 5;
    public OutboxStatus Status { get; set; } = OutboxStatus.Queued;
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LockedUntilUtc { get; set; }
}


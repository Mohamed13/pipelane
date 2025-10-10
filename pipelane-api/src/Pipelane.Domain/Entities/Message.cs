using Pipelane.Domain.Enums;

namespace Pipelane.Domain.Entities;

public class Message : BaseEntity
{
    public Guid ConversationId { get; set; }
    public Channel Channel { get; set; }
    public MessageDirection Direction { get; set; }
    public MessageType Type { get; set; }
    public string? TemplateName { get; set; }
    public string? Lang { get; set; }
    public string? PayloadJson { get; set; }
    public MessageStatus Status { get; set; }
    public string? Provider { get; set; }
    public string? ProviderMessageId { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorReason { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? OpenedAt { get; set; }
    public DateTime? FailedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<MessageEvent> Events { get; set; } = new List<MessageEvent>();
}

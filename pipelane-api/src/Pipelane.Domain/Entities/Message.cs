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
    public string? ProviderMessageId { get; set; }
    public DateTime CreatedAt { get; set; }
}


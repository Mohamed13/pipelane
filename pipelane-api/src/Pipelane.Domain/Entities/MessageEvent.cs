using Pipelane.Domain.Enums;

namespace Pipelane.Domain.Entities;

public class MessageEvent : BaseEntity
{
    public Guid MessageId { get; set; }
    public MessageEventType Type { get; set; }
    public string? Provider { get; set; }
    public string? ProviderEventId { get; set; }
    public string? Raw { get; set; }
    public DateTime CreatedAt { get; set; }

    public Message? Message { get; set; }
}

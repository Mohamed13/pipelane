using Pipelane.Domain.Enums;

namespace Pipelane.Domain.Entities;

public class Conversation : BaseEntity
{
    public Guid ContactId { get; set; }
    public Channel PrimaryChannel { get; set; }
    public string? ProviderThreadId { get; set; }
    public DateTime CreatedAt { get; set; }
}


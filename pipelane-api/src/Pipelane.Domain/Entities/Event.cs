using Pipelane.Domain.Enums;

namespace Pipelane.Domain.Entities;

public class Event : BaseEntity
{
    public EventSource Source { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
}


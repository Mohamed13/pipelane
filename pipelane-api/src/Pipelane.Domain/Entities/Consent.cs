using Pipelane.Domain.Enums;

namespace Pipelane.Domain.Entities;

public class Consent : BaseEntity
{
    public Guid ContactId { get; set; }
    public Channel Channel { get; set; }
    public DateTime OptInAtUtc { get; set; }
    public string? Source { get; set; }
    public string? MetaJson { get; set; }
}


using Pipelane.Domain.Enums;

namespace Pipelane.Domain.Entities;

public class Template : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public Channel Channel { get; set; }
    public string Lang { get; set; } = "en";
    public string? Category { get; set; }
    public string CoreSchemaJson { get; set; } = "{}";
    public bool IsActive { get; set; } = true;
    public DateTime UpdatedAtUtc { get; set; }
}


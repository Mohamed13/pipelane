namespace Pipelane.Domain.Entities;

public sealed class RateLimitSnapshot : BaseEntity
{
    public Guid TargetTenantId { get; set; }
    public string Scope { get; set; } = "send";
    public string HitsJson { get; set; } = "[]";
    public DateTime WindowStartUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

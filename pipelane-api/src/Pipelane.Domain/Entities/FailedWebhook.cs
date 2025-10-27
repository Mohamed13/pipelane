using Pipelane.Domain.Enums;

namespace Pipelane.Domain.Entities;

public sealed class FailedWebhook : BaseEntity
{
    public Channel Channel { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string HeadersJson { get; set; } = "{}";
    public string? LastError { get; set; }
    public int RetryCount { get; set; }
    public DateTime NextAttemptUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

namespace Pipelane.Infrastructure.Automations;

public sealed class AutomationsOptions
{
    public bool EventsEnabled { get; set; }
    public string? EventsUrl { get; set; }
    public string? Token { get; set; }
    public bool ActionsEnabled { get; set; }
    public int RateLimitPerMinute { get; set; } = 300;
}

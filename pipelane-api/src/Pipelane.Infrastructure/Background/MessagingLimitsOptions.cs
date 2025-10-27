namespace Pipelane.Infrastructure.Background;

public sealed class MessagingLimitsOptions
{
    public int DailySendCap { get; set; } = 100;
    public TimeSpan QuietHoursStart { get; set; } = TimeSpan.FromHours(22);
    public TimeSpan QuietHoursEnd { get; set; } = TimeSpan.FromHours(8);
    public int PerMinuteGlobal { get; set; } = 120;
    public int PerMinutePerTenant { get; set; } = 60;
    public int WebhookPerMinutePerTenant { get; set; } = 600;
}

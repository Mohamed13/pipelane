namespace Pipelane.Application.Analytics;

public sealed record DeliveryAnalyticsResult(
    DeliveryTotals Totals,
    IReadOnlyCollection<DeliveryChannelBreakdown> ByChannel,
    IReadOnlyCollection<DeliveryTemplateBreakdown> ByTemplate);

public sealed record DeliveryTotals(
    int Queued,
    int Sent,
    int Delivered,
    int Opened,
    int Failed,
    int Bounced);

public sealed record DeliveryChannelBreakdown(
    string Channel,
    int Queued,
    int Sent,
    int Delivered,
    int Opened,
    int Failed,
    int Bounced);

public sealed record DeliveryTemplateBreakdown(
    string Template,
    string Channel,
    int Queued,
    int Sent,
    int Delivered,
    int Opened,
    int Failed,
    int Bounced);

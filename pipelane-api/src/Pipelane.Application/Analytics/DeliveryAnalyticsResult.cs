namespace Pipelane.Application.Analytics;

public sealed record DeliveryAnalyticsResult(
    DeliveryTotals Totals,
    IReadOnlyCollection<DeliveryChannelBreakdown> ByChannel,
    IReadOnlyCollection<DeliveryTemplateBreakdown> ByTemplate,
    IReadOnlyCollection<DeliveryTimelinePoint> Timeline);

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

public sealed record DeliveryTimelinePoint(
    DateTime Date,
    int Queued,
    int Sent,
    int Delivered,
    int Opened,
    int Failed,
    int Bounced);

public sealed record TopMessagesResult(
    DateTime From,
    DateTime To,
    IReadOnlyCollection<TopMessageItem> TopByReplies,
    IReadOnlyCollection<TopMessageItem> TopByOpens);

public sealed record TopMessageItem(
    string Key,
    string Label,
    string Channel,
    int Sent,
    int Delivered,
    int Opened,
    int Failed,
    int Bounced,
    int Replies);

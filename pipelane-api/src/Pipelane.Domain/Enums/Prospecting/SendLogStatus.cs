namespace Pipelane.Domain.Enums.Prospecting;

public enum SendLogStatus
{
    Scheduled = 0,
    PendingSend = 1,
    Sent = 2,
    Delivered = 3,
    Opened = 4,
    Clicked = 5,
    Deferred = 6,
    Bounced = 7,
    Complained = 8,
    Failed = 9,
    Cancelled = 10
}

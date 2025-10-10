namespace Pipelane.Domain.Enums;

public enum MessageStatus
{
    Queued = 0,
    Sent = 1,
    Delivered = 2,
    Opened = 3,
    Failed = 4,
    Bounced = 5
}

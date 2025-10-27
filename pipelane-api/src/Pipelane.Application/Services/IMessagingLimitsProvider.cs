using System;

namespace Pipelane.Application.Services;

public interface IMessagingLimitsProvider
{
    MessagingLimitsSnapshot GetLimits();
}

public sealed record MessagingLimitsSnapshot(int DailySendCap, TimeSpan QuietHoursStart, TimeSpan QuietHoursEnd);

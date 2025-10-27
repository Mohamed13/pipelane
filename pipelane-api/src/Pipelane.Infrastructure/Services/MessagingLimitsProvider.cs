using Microsoft.Extensions.Options;

using Pipelane.Application.Services;
using Pipelane.Infrastructure.Background;

namespace Pipelane.Infrastructure.Services;

public sealed class MessagingLimitsProvider : IMessagingLimitsProvider
{
    private readonly IOptions<MessagingLimitsOptions> _options;

    public MessagingLimitsProvider(IOptions<MessagingLimitsOptions> options)
    {
        _options = options;
    }

    /// <inheritdoc/>
    public MessagingLimitsSnapshot GetLimits()
    {
        var value = _options.Value;
        return new MessagingLimitsSnapshot(value.DailySendCap, value.QuietHoursStart, value.QuietHoursEnd);
    }
}

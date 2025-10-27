using System.Linq;

using Microsoft.Extensions.Logging;

using Pipelane.Application.Abstractions;
using Pipelane.Domain.Enums;

using Quartz;

namespace Pipelane.Infrastructure.Webhooks;

public sealed class WebhookRetryJob : IJob
{
    private readonly IWebhookDeadLetterStore _deadLetterStore;
    private readonly IEnumerable<IMessageChannel> _channels;
    private readonly ILogger<WebhookRetryJob> _logger;

    public WebhookRetryJob(
        IWebhookDeadLetterStore deadLetterStore,
        IEnumerable<IMessageChannel> channels,
        ILogger<WebhookRetryJob> logger)
    {
        _deadLetterStore = deadLetterStore;
        _channels = channels;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var cancellationToken = context?.CancellationToken ?? CancellationToken.None;
        var now = DateTime.UtcNow;
        var due = await _deadLetterStore.TakeDueAsync(now, 50, cancellationToken).ConfigureAwait(false);
        if (due.Count == 0)
        {
            return;
        }

        foreach (var item in due)
        {
            var headers = new Dictionary<string, string>(item.Headers, StringComparer.OrdinalIgnoreCase);
            var channel = ResolveChannel(item.Channel);
            try
            {
                var result = await channel.HandleWebhookAsync(item.Payload, headers, cancellationToken).ConfigureAwait(false);
                if (result.Ok)
                {
                    await _deadLetterStore.MarkSuccessAsync(item.Id, cancellationToken).ConfigureAwait(false);
                    _logger.LogInformation("Webhook retry succeeded channel={Channel} provider={Provider}", item.Channel, channel.GetType().Name);
                }
                else
                {
                    var reason = result.Reason ?? "unknown_error";
                    await _deadLetterStore.MarkFailureAsync(item.Id, reason, ComputeBackoff(item.RetryCount), cancellationToken).ConfigureAwait(false);
                    _logger.LogWarning("Webhook retry failed channel={Channel} reason={Reason}", item.Channel, reason);
                }
            }
            catch (Exception ex)
            {
                await _deadLetterStore.MarkFailureAsync(item.Id, ex.Message, ComputeBackoff(item.RetryCount), cancellationToken).ConfigureAwait(false);
                _logger.LogError(ex, "Webhook retry crashed channel={Channel}", item.Channel);
            }
        }
    }

    private IMessageChannel ResolveChannel(Channel channel)
        => _channels.First(c => c.Channel == channel);

    private static TimeSpan ComputeBackoff(int retryCount)
    {
        var minutes = Math.Pow(2, retryCount + 1);
        minutes = Math.Clamp(minutes, 1, 60);
        return TimeSpan.FromMinutes(minutes);
    }
}

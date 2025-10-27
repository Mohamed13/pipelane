using Microsoft.Extensions.Logging;

using Quartz;

namespace Pipelane.Infrastructure.Background;

public sealed class SendDueMessagesJob : IJob
{
    private readonly OutboxDispatchExecutor _executor;
    private readonly ILogger<SendDueMessagesJob> _logger;

    public SendDueMessagesJob(OutboxDispatchExecutor executor, ILogger<SendDueMessagesJob> logger)
    {
        _executor = executor;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            var processed = await _executor.DispatchAsync(25, context.CancellationToken);
            if (processed > 0)
            {
                _logger.LogInformation("Dispatched {Count} messages from scheduled job", processed);
            }
        }
        catch (OperationCanceledException)
        {
            // job cancelled, ignore
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while dispatching due messages");
        }
    }
}

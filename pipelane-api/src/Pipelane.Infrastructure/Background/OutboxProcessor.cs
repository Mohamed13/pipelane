using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Pipelane.Infrastructure.Background;

public class OutboxProcessor : BackgroundService
{
    private readonly OutboxDispatchExecutor _executor;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(OutboxDispatchExecutor executor, ILogger<OutboxProcessor> logger)
    {
        _executor = executor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _executor.DispatchAsync(10, stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox loop error");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

}




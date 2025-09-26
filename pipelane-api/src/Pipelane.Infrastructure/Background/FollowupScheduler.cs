using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pipelane.Application.Storage;
using Pipelane.Domain.Entities;

namespace Pipelane.Infrastructure.Background;

public class FollowupScheduler : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<FollowupScheduler> _logger;

    public FollowupScheduler(IServiceProvider sp, ILogger<FollowupScheduler> logger)
    { _sp = sp; _logger = logger; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

                // Placeholder: compute follow-ups for contacts without conversions
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Followup scheduler error");
                await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
            }
        }
    }
}


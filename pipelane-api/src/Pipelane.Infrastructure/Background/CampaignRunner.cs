using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Pipelane.Application.Services;
using Pipelane.Application.Storage;
using Pipelane.Domain.Entities;
using Pipelane.Domain.Enums;

namespace Pipelane.Infrastructure.Background;

public class CampaignRunner : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<CampaignRunner> _logger;

    public CampaignRunner(IServiceProvider sp, ILogger<CampaignRunner> logger)
    { _sp = sp; _logger = logger; }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
                var outbox = scope.ServiceProvider.GetRequiredService<IOutboxService>();

                var now = DateTime.UtcNow;
                var due = await db.Campaigns
                    .Where(c => (c.Status == CampaignStatus.Pending || c.Status == CampaignStatus.Running) && (c.ScheduledAtUtc == null || c.ScheduledAtUtc <= now))
                    .Take(5)
                    .ToListAsync(stoppingToken);

                foreach (var camp in due)
                {
                    camp.Status = CampaignStatus.Running;

                    // very simple segment: all contacts for now or filter by tag in segment_json
                    var contacts = await db.Contacts.Take(100).ToListAsync(stoppingToken);
                    foreach (var contact in contacts)
                    {
                        var msg = new OutboxMessage
                        {
                            Id = Guid.NewGuid(),
                            TenantId = camp.TenantId,
                            ContactId = contact.Id,
                            Channel = camp.PrimaryChannel,
                            Type = MessageType.Template,
                            TemplateId = camp.TemplateId,
                            PayloadJson = camp.SegmentJson, // placeholder: ideally variables per contact
                            CreatedAt = DateTime.UtcNow
                        };
                        await outbox.EnqueueAsync(msg, stoppingToken);
                    }

                    camp.Status = CampaignStatus.Done;
                    await db.SaveChangesAsync(stoppingToken);
                }

                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Campaign runner error");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }
}


using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Pipelane.Application.Storage;
using Pipelane.Domain.Entities;
using Pipelane.Domain.Enums;

namespace Pipelane.Api.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController : ControllerBase
{
    private readonly IAppDbContext _db;

    public HealthController(IAppDbContext db)
    {
        _db = db;
    }

    [HttpGet("metrics")]
    public async Task<IActionResult> GetMetrics(CancellationToken ct)
    {
        var queueDepth = await _db.Outbox
            .Where(o => o.Status == OutboxStatus.Queued)
            .CountAsync(ct)
            .ConfigureAwait(false);

        var samplingWindow = DateTime.UtcNow.AddHours(-24);

        var recentDeliveries = await _db.Messages
            .Where(m => m.Direction == MessageDirection.Out && m.DeliveredAt != null && m.CreatedAt >= samplingWindow)
            .OrderByDescending(m => m.CreatedAt)
            .Take(200)
            .Select(m => new { m.CreatedAt, m.DeliveredAt })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var latencySeconds = recentDeliveries.Count == 0
            ? 0d
            : recentDeliveries.Average(m => (m.DeliveredAt!.Value - m.CreatedAt).TotalSeconds);

        var failedWebhooks = await _db.FailedWebhooks
            .Where(f => f.CreatedAtUtc >= samplingWindow)
            .CountAsync(ct)
            .ConfigureAwait(false);
        var processedWebhooks = await _db.MessageEvents
            .Where(e => e.CreatedAt >= samplingWindow && e.Provider != null)
            .CountAsync(ct)
            .ConfigureAwait(false);

        var totalWebhooks = failedWebhooks + processedWebhooks;
        var errorRate = totalWebhooks == 0 ? 0d : failedWebhooks / (double)totalWebhooks;

        return Ok(new
        {
            queueDepth,
            avgSendLatencySeconds = Math.Round(latencySeconds, 2),
            webhookErrorRate = Math.Round(errorRate, 4)
        });
    }
}

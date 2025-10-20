using System.Linq;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Pipelane.Application.Storage;
using Pipelane.Domain.Entities.Prospecting;
using Pipelane.Domain.Enums.Prospecting;

namespace Pipelane.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/prospecting/hooks")]
public class ProspectingAutomationController : ControllerBase
{
    private readonly IAppDbContext _db;
    private readonly ILogger<ProspectingAutomationController> _logger;

    public ProspectingAutomationController(IAppDbContext db, ILogger<ProspectingAutomationController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpPost("enrich")]
    public async Task<IActionResult> EnrichProspects(CancellationToken ct)
    {
        var prospects = await _db.Prospects
            .Where(p => p.EnrichedJson == null)
            .Take(20)
            .ToListAsync(ct);
        var now = DateTime.UtcNow;
        foreach (var prospect in prospects)
        {
            var enrichment = new
            {
                prospect.Company,
                prospect.Title,
                enrichedAt = now.ToString("o"),
                source = "n8n-demo"
            };
            prospect.EnrichedJson = System.Text.Json.JsonSerializer.Serialize(enrichment);
            prospect.LastSyncedAtUtc = now;
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new { enriched = prospects.Count });
    }

    [HttpPost("send-next")]
    public async Task<IActionResult> SendDueEmails(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var due = await _db.ProspectingSendLogs
            .Where(l => l.Status == SendLogStatus.Scheduled && l.ScheduledAtUtc <= now)
            .OrderBy(l => l.ScheduledAtUtc)
            .Take(50)
            .ToListAsync(ct);

        foreach (var log in due)
        {
            log.Status = SendLogStatus.Sent;
            log.SentAtUtc = now;
            log.Provider = "sendgrid";
            log.ProviderMessageId ??= $"sg_{Guid.NewGuid():N}";
            log.UpdatedAtUtc = now;
        }

        var prospectIds = due.Select(l => l.ProspectId).Distinct().ToList();
        if (prospectIds.Count > 0)
        {
            var prospects = await _db.Prospects.Where(p => prospectIds.Contains(p.Id)).ToListAsync(ct);
            foreach (var prospect in prospects)
            {
                prospect.LastContactedAtUtc = now;
                if (prospect.Status == ProspectStatus.New)
                {
                    prospect.Status = ProspectStatus.Active;
                }
            }
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new { processed = due.Count });
    }

    [HttpPost("follow-up")]
    public async Task<IActionResult> ScheduleFollowUps(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var prospects = await _db.Prospects
            .Where(p => !p.OptedOut && p.LastContactedAtUtc != null && p.LastRepliedAtUtc == null && p.LastContactedAtUtc < now.AddDays(-3))
            .Take(50)
            .ToListAsync(ct);

        foreach (var prospect in prospects)
        {
            var followUp = new SendLog
            {
                Id = Guid.NewGuid(),
                TenantId = prospect.TenantId,
                ProspectId = prospect.Id,
                Provider = "sendgrid",
                Status = SendLogStatus.Scheduled,
                ScheduledAtUtc = now.AddHours(2),
                CreatedAtUtc = now,
                MetadataJson = "{\"kind\":\"n8n-followup\"}"
            };
            await _db.ProspectingSendLogs.AddAsync(followUp, ct);
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new { followUps = prospects.Count });
    }
}

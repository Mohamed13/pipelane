using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pipelane.Application.DTOs;
using Pipelane.Application.Storage;
using Pipelane.Domain.Entities;

namespace Pipelane.Api.Controllers;

[ApiController]
[Microsoft.AspNetCore.Authorization.Authorize]
[Route("campaigns")] 
public class CampaignsController : ControllerBase
{
    private readonly IAppDbContext _db;
    private readonly Pipelane.Infrastructure.Persistence.ITenantProvider _tenant;
    public CampaignsController(IAppDbContext db, Pipelane.Infrastructure.Persistence.ITenantProvider tenant) { _db = db; _tenant = tenant; }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCampaignRequest req, CancellationToken ct)
    {
        var c = new Campaign
        {
            Id = Guid.NewGuid(),
            TenantId = _tenant.TenantId,
            Name = req.Name,
            PrimaryChannel = req.PrimaryChannel,
            FallbackOrderJson = req.FallbackOrderJson,
            TemplateId = req.TemplateId,
            SegmentJson = req.SegmentJson,
            ScheduledAtUtc = req.ScheduledAtUtc,
            Status = Domain.Enums.CampaignStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        _db.Campaigns.Add(c);
        await _db.SaveChangesAsync(ct);
        return Ok(new { id = c.Id });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var c = await _db.Campaigns.FirstOrDefaultAsync(x => x.Id == id, ct);
        return c is null ? NotFound() : Ok(c);
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pipelane.Application.Storage;
using Pipelane.Domain.Enums;

namespace Pipelane.Api.Controllers;

[ApiController]
[Microsoft.AspNetCore.Authorization.Authorize]
[Route("analytics")] 
public class AnalyticsController : ControllerBase
{
    private readonly IAppDbContext _db;
    public AnalyticsController(IAppDbContext db) => _db = db;

    [HttpGet("overview")]
    public async Task<IActionResult> Overview([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var start = from ?? DateTime.UtcNow.AddDays(-7);
        var end = to ?? DateTime.UtcNow;
        var q = _db.Messages.Where(m => m.CreatedAt >= start && m.CreatedAt <= end);
        var total = await q.CountAsync(ct);
        var byChannel = await q.GroupBy(m => m.Channel).Select(g => new { channel = g.Key.ToString().ToLowerInvariant(), count = g.Count() }).ToListAsync(ct);
        return Ok(new { total, byChannel });
    }
}

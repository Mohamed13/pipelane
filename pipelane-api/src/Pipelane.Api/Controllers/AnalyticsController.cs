using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Pipelane.Application.Analytics;
using Pipelane.Application.Services;
using Pipelane.Application.Storage;
using Pipelane.Domain.Enums;

namespace Pipelane.Api.Controllers;

[ApiController]
[Microsoft.AspNetCore.Authorization.Authorize]
[Route("analytics")]
public class AnalyticsController : ControllerBase
{
    private readonly IAppDbContext _db;
    private readonly IAnalyticsService _analytics;

    public AnalyticsController(IAppDbContext db, IAnalyticsService analytics)
    {
        _db = db;
        _analytics = analytics;
    }

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

    [HttpGet("delivery")]
    public async Task<ActionResult<DeliveryAnalyticsResult>> Delivery([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var start = from ?? DateTime.UtcNow.AddDays(-7);
        var end = to ?? DateTime.UtcNow;
        var result = await _analytics.GetDeliveryAsync(start, end, ct);
        return Ok(result);
    }

    [HttpGet("top-messages")]
    public async Task<ActionResult<TopMessagesResult>> TopMessages([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var start = from ?? DateTime.UtcNow.AddDays(-7);
        var end = to ?? DateTime.UtcNow;
        var result = await _analytics.GetTopMessagesAsync(start, end, ct);
        return Ok(result);
    }
}

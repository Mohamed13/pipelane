using Microsoft.AspNetCore.Mvc;

using Pipelane.Application.DTOs;
using Pipelane.Application.Storage;
using Pipelane.Domain.Entities;

namespace Pipelane.Api.Controllers;

[ApiController]
[Microsoft.AspNetCore.Authorization.Authorize]
[Route("conversions")]
public class ConversionsController : ControllerBase
{
    private readonly IAppDbContext _db;
    private readonly Pipelane.Infrastructure.Persistence.ITenantProvider _tenant;
    public ConversionsController(IAppDbContext db, Pipelane.Infrastructure.Persistence.ITenantProvider tenant) { _db = db; _tenant = tenant; }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ConversionRequest req, CancellationToken ct)
    {
        _db.Conversions.Add(new Conversion
        {
            Id = Guid.NewGuid(),
            TenantId = _tenant.TenantId,
            ContactId = req.ContactId,
            Amount = req.Amount,
            Currency = req.Currency,
            OrderId = req.OrderId,
            RevenueAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
        return Ok();
    }
}

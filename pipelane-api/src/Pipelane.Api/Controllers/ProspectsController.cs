using Microsoft.AspNetCore.Mvc;

using Pipelane.Application.Prospecting;
using Pipelane.Infrastructure.Persistence;

namespace Pipelane.Api.Controllers;

[ApiController]
[Microsoft.AspNetCore.Authorization.Authorize]
[Route("api/prospects")]
public class ProspectsController : ControllerBase
{
    private readonly IProspectingService _service;
    private readonly ITenantProvider _tenant;

    public ProspectsController(IProspectingService service, ITenantProvider tenant)
    {
        _service = service;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<IActionResult> GetProspects([FromQuery] int page = 1, [FromQuery] int size = 50, [FromQuery] string? search = null, CancellationToken ct = default)
    {
        var (total, items) = await _service.GetProspectsAsync(page, size, search, ct);
        return Ok(new { total, items });
    }

    [HttpPost("import")]
    public async Task<ActionResult<ProspectImportResult>> Import(ProspectImportRequest request, CancellationToken ct)
    {
        var result = await _service.ImportProspectsAsync(_tenant.TenantId, request, ct);
        return Ok(result);
    }

    [HttpPost("optout")]
    public async Task<IActionResult> OptOut([FromQuery] string email, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest("Email is required.");
        }

        var prospect = await _service.UpdateOptOutAsync(email, ct);
        if (prospect is null)
        {
            return NotFound();
        }

        return Ok(prospect);
    }
}

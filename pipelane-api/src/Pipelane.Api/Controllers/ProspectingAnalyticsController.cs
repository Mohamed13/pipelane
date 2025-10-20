using Microsoft.AspNetCore.Mvc;

using Pipelane.Application.Prospecting;

namespace Pipelane.Api.Controllers;

[ApiController]
[Microsoft.AspNetCore.Authorization.Authorize]
[Route("api/prospecting/analytics")]
public class ProspectingAnalyticsController : ControllerBase
{
    private readonly IProspectingService _service;

    public ProspectingAnalyticsController(IProspectingService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<ProspectingAnalyticsResponse>> Get([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var start = from ?? DateTime.UtcNow.AddDays(-14);
        var end = to ?? DateTime.UtcNow;
        var result = await _service.GetAnalyticsAsync(start, end, ct);
        return Ok(result);
    }
}

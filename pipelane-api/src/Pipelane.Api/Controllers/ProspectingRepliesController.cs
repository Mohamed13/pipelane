using Microsoft.AspNetCore.Mvc;

using Pipelane.Application.Prospecting;
using Pipelane.Domain.Enums.Prospecting;

namespace Pipelane.Api.Controllers;

[ApiController]
[Microsoft.AspNetCore.Authorization.Authorize]
[Route("api/prospecting/replies")]
public class ProspectingRepliesController : ControllerBase
{
    private readonly IProspectingService _service;

    public ProspectingRepliesController(IProspectingService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ReplyIntent? intent, CancellationToken ct)
    {
        var replies = await _service.GetRepliesAsync(intent, ct);
        return Ok(replies);
    }
}




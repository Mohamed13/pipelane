using Microsoft.AspNetCore.Mvc;
using Pipelane.Application.DTOs;
using Pipelane.Application.Services;

namespace Pipelane.Api.Controllers;

[ApiController]
[Microsoft.AspNetCore.Authorization.Authorize]
[Route("messages")] 
public class MessagesController : ControllerBase
{
    private readonly IMessagingService _svc;
    public MessagesController(IMessagingService svc) => _svc = svc;

    [HttpPost("send")]
    public async Task<IActionResult> Send([FromBody] SendMessageRequest req, CancellationToken ct)
    {
        var res = await _svc.SendAsync(req, ct);
        if (!res.Success) return Problem(detail: res.Error, statusCode: 422);
        return Ok(res);
    }
}

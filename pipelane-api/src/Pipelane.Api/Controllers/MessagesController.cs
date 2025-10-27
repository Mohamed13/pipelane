using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Pipelane.Application.DTOs;
using Pipelane.Application.Services;
using Pipelane.Infrastructure.Background;
using Pipelane.Infrastructure.Persistence;

namespace Pipelane.Api.Controllers;

[ApiController]
[Microsoft.AspNetCore.Authorization.Authorize]
[Route("messages")]
public class MessagesController : ControllerBase
{
    private readonly IMessagingService _svc;
    private readonly ITenantProvider _tenantProvider;
    private readonly IMessageSendRateLimiter _rateLimiter;

    public MessagesController(
        IMessagingService svc,
        ITenantProvider tenantProvider,
        IMessageSendRateLimiter rateLimiter)
    {
        _svc = svc;
        _tenantProvider = tenantProvider;
        _rateLimiter = rateLimiter;
    }

    [HttpPost("send")]
    public async Task<IActionResult> Send([FromBody] SendMessageRequest req, CancellationToken ct)
    {
        if (!await _rateLimiter.TryAcquireAsync(_tenantProvider.TenantId, ct).ConfigureAwait(false))
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new ProblemDetails
            {
                Title = "rate_limited",
                Detail = "Message send limit reached. Try again in a minute.",
                Status = StatusCodes.Status429TooManyRequests
            });
        }

        var res = await _svc.SendAsync(req, ct);
        if (!res.Success) return Problem(detail: res.Error, statusCode: 422);
        return Ok(res);
    }
}

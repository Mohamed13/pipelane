using Microsoft.AspNetCore.Mvc;

using Pipelane.Application.Prospecting;
using Pipelane.Infrastructure.Persistence;

namespace Pipelane.Api.Controllers;

[ApiController]
[Microsoft.AspNetCore.Authorization.Authorize]
[Route("api/prospecting/ai")]
public class ProspectingAiController : ControllerBase
{
    private readonly IProspectingService _service;
    private readonly ITenantProvider _tenant;

    public ProspectingAiController(IProspectingService service, ITenantProvider tenant)
    {
        _service = service;
        _tenant = tenant;
    }

    [HttpPost("generate-email")]
    public async Task<ActionResult<GenerateEmailResponse>> GenerateEmail(GenerateEmailRequest request, CancellationToken ct)
    {
        var response = await _service.GenerateEmailAsync(_tenant.TenantId, request, ct);
        return Ok(response);
    }

    [HttpPost("classify-reply")]
    public async Task<ActionResult<ProspectingClassifyReplyResponse>> ClassifyReply(ProspectingClassifyReplyRequest request, CancellationToken ct)
    {
        var response = await _service.ClassifyReplyAsync(request, ct);
        return Ok(response);
    }

    [HttpPost("auto-reply")]
    public async Task<ActionResult<AutoReplyResponse>> AutoReply(AutoReplyRequest request, CancellationToken ct)
    {
        var response = await _service.AutoReplyAsync(_tenant.TenantId, request, ct);
        return Ok(response);
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using Pipelane.Application.Services;
using Pipelane.Application.Storage;
using Pipelane.Infrastructure.Demo;
using Pipelane.Infrastructure.Persistence;

namespace Pipelane.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/demo")]
public sealed class DemoController : ControllerBase
{
    private readonly IDemoExperienceService _demoService;
    private readonly ITenantProvider _tenantProvider;
    private readonly IOptions<DemoOptions> _options;
    private readonly ILogger<DemoController> _logger;

    public DemoController(
        IDemoExperienceService demoService,
        ITenantProvider tenantProvider,
        IOptions<DemoOptions> options,
        ILogger<DemoController> logger)
    {
        _demoService = demoService;
        _tenantProvider = tenantProvider;
        _options = options;
        _logger = logger;
    }

    [HttpPost("run")]
    public async Task<ActionResult<DemoRunResult>> Run(CancellationToken ct)
    {
        if (!_options.Value.Enabled)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Title = "Demo mode disabled",
                Status = StatusCodes.Status503ServiceUnavailable,
                Detail = "Enable DEMO_MODE=true to trigger demo runs."
            });
        }

        var tenantId = _tenantProvider.TenantId;
        if (tenantId == Guid.Empty)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "tenant_missing",
                Status = StatusCodes.Status400BadRequest,
                Detail = "Provide X-Tenant-Id header to launch the demo."
            });
        }

        var result = await _demoService.RunAsync(tenantId, ct).ConfigureAwait(false);
        _logger.LogInformation(
            "Demo run triggered for tenant {TenantId} by {User} ({MessageCount} messages)",
            tenantId,
            User.Identity?.Name ?? "anonymous",
            result.Messages.Count);

        return Ok(result);
    }
}



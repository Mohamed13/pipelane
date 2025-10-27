using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Pipelane.Application.Hunter;
using Pipelane.Infrastructure.Persistence;

namespace Pipelane.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/cadences")]
public class CadencesController : ControllerBase
{
    private readonly IHunterService _service;
    private readonly ITenantProvider _tenantProvider;

    public CadencesController(IHunterService service, ITenantProvider tenantProvider)
    {
        _service = service;
        _tenantProvider = tenantProvider;
    }

    [HttpPost("from-list")]
    public async Task<ActionResult<object>> CreateFromList(CadenceFromListRequest request, CancellationToken ct)
    {
        var cadenceId = await _service.CreateCadenceFromListAsync(_tenantProvider.TenantId, request, ct);
        return Ok(new { cadenceId });
    }
}

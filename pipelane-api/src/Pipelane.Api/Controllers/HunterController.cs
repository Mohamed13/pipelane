using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Pipelane.Application.Hunter;
using Pipelane.Application.Services;
using Pipelane.Infrastructure.Persistence;

namespace Pipelane.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/hunter")]
public class HunterController : ControllerBase
{
    private readonly IHunterService _service;
    private readonly IHunterDemoSeeder _demoSeeder;
    private readonly ITenantProvider _tenantProvider;

    public HunterController(IHunterService service, IHunterDemoSeeder demoSeeder, ITenantProvider tenantProvider)
    {
        _service = service;
        _demoSeeder = demoSeeder;
        _tenantProvider = tenantProvider;
    }

    [HttpPost("upload-csv")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<object>> UploadCsv(IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("Fichier CSV manquant.");
        }

        await using var stream = file.OpenReadStream();
        var csvId = await _service.UploadCsvAsync(
            _tenantProvider.TenantId,
            new StreamReference(file.FileName, stream),
            ct);

        return Ok(new { csvId });
    }

    [HttpPost("search")]
    public async Task<ActionResult<HunterSearchResponse>> Search(HunterSearchCriteria criteria, [FromQuery] bool dryRun, CancellationToken ct)
    {
        var response = await _service.SearchAsync(_tenantProvider.TenantId, criteria, dryRun, ct);
        return Ok(response);
    }

    [HttpPost("seed-demo")]
    public async Task<ActionResult<HunterSearchResponse>> SeedDemo(CancellationToken ct)
    {
        var items = await _demoSeeder.SeedAsync(_tenantProvider.TenantId, ct);
        var response = new HunterSearchResponse(items.Count, 0, items);
        return Ok(response);
    }
}

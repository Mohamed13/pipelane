using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Pipelane.Api.Middleware;
using Pipelane.Application.Hunter;
using Pipelane.Application.Services;
using Pipelane.Infrastructure.Persistence;

namespace Pipelane.Api.Controllers;

/// <summary>
/// Expose les opérations Lead Hunter : import CSV, recherche enrichie et scénarios de démonstration.
/// </summary>
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

    /// <summary>
    /// Dépose un fichier CSV temporaire afin d'alimenter la recherche Lead Hunter.
    /// </summary>
    /// <param name="file">Fichier CSV contenant des prospects.</param>
    /// <param name="ct">Token d'annulation.</param>
    /// <returns>Identifiant du dépôt.</returns>
    /// <response code="200">Le fichier a été stocké et peut être réutilisé dans une recherche.</response>
    /// <response code="400">Le fichier est manquant ou vide.</response>
    [HttpPost("upload-csv")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<object>> UploadCsv(IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = ProblemDetailsFrenchLocalizer.ResolveTitle(StatusCodes.Status400BadRequest),
                Detail = "Fichier CSV manquant ou vide.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        await using var stream = file.OpenReadStream();
        var csvId = await _service.UploadCsvAsync(
            _tenantProvider.TenantId,
            new StreamReference(file.FileName, stream),
            ct);

        return Ok(new { csvId });
    }

    /// <summary>
    /// Lance une recherche Lead Hunter et retourne les prospects enrichis/scorés.
    /// </summary>
    /// <param name="criteria">Critères d'industrie, géolocalisation et filtres avancés.</param>
    /// <param name="dryRun">Lorsque vrai, n'enregistre pas les prospects en base.</param>
    /// <param name="ct">Token d'annulation.</param>
    /// <returns>Résultats scorés et motifs de sélection.</returns>
    /// <response code="200">Résultats disponibles.</response>
    [HttpPost("search")]
    public async Task<ActionResult<HunterSearchResponse>> Search(HunterSearchCriteria criteria, [FromQuery] bool dryRun, CancellationToken ct)
    {
        var response = await _service.SearchAsync(_tenantProvider.TenantId, criteria, dryRun, ct);
        return Ok(response);
    }

    /// <summary>
    /// Précharge 50 prospects de démonstration (mode DEMO).
    /// </summary>
    /// <param name="ct">Token d'annulation.</param>
    /// <returns>Prospects de démonstration prêts à l'emploi.</returns>
    /// <response code="200">Prospects créés pour le tenant courant.</response>
    [HttpPost("seed-demo")]
    public async Task<ActionResult<HunterSearchResponse>> SeedDemo(CancellationToken ct)
    {
        var items = await _demoSeeder.SeedAsync(_tenantProvider.TenantId, ct);
        var response = new HunterSearchResponse(items.Count, 0, items);
        return Ok(response);
    }
}

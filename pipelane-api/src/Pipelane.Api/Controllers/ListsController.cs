using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using Pipelane.Api.Middleware;
using Pipelane.Application.Hunter;
using Pipelane.Infrastructure.Persistence;

namespace Pipelane.Api.Controllers;

/// <summary>
/// Gère les listes de prospects Lead Hunter (création, lecture, renommage, suppression).
/// </summary>
[ApiController]
[Authorize]
[Route("api/lists")]
public class ListsController : ControllerBase
{
    private readonly IHunterService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly IDatabaseDiagnostics _dbDiagnostics;
    private readonly ILogger<ListsController> _logger;

    public ListsController(
        IHunterService service,
        ITenantProvider tenantProvider,
        IDatabaseDiagnostics dbDiagnostics,
        ILogger<ListsController> logger)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _dbDiagnostics = dbDiagnostics;
        _logger = logger;
    }

    /// <summary>
    /// Crée une nouvelle liste Lead Hunter pour le tenant courant.
    /// </summary>
    /// <param name="request">Nom de la liste à créer.</param>
    /// <param name="ct">Token d'annulation.</param>
    /// <returns>Identifiant de la liste créée.</returns>
    /// <response code="200">La liste est créée.</response>
    /// <response code="400">Le tenant est manquant ou le nom vide.</response>
    [HttpPost]
    public async Task<ActionResult<object>> Create(CreateListRequest request, CancellationToken ct)
    {
        if (!TryGetTenantId(out var tenantId, out var problem))
        {
            return BadRequest(problem);
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new ProblemDetails
            {
                Title = ProblemDetailsFrenchLocalizer.ResolveTitle(StatusCodes.Status400BadRequest),
                Detail = "Le nom de la liste est obligatoire.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var id = await _service.CreateListAsync(tenantId, request, ct);
        return Ok(new { id });
    }

    /// <summary>
    /// Retourne l'ensemble des listes du tenant courant.
    /// </summary>
    /// <param name="ct">Token d'annulation.</param>
    /// <returns>Listes disponibles.</returns>
    /// <response code="200">Listes récupérées avec succès.</response>
    /// <response code="400">Tenant manquant.</response>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProspectListSummary>>> GetAll(CancellationToken ct)
    {
        if (!TryGetTenantId(out var tenantId, out var problem))
        {
            return BadRequest(problem);
        }

        var userId = GetUserId();
        _logger.LogInformation("Chargement des listes pour le tenant {TenantId} (utilisateur {UserId}, provider {Provider})", tenantId, userId, _dbDiagnostics.ProviderName);

        try
        {
            var items = await _service.GetListsAsync(tenantId, ct);
            _logger.LogInformation("Chargement terminé ({Count} listes) pour le tenant {TenantId}", items.Count, tenantId);
            return Ok(items);
        }
        catch (Exception ex)
        {
            var pending = await _dbDiagnostics.GetPendingMigrationsAsync(ct);
            if (pending.Count > 0)
            {
                _logger.LogError(ex, "Migrations en attente détectées pour le tenant {TenantId}: {PendingMigrations}", tenantId, pending);
                var migrationProblem = new ProblemDetails
                {
                    Title = "Migrations en attente",
                    Detail = "La base de données nécessite l'application des dernières migrations.",
                    Status = StatusCodes.Status503ServiceUnavailable
                };
                return StatusCode(StatusCodes.Status503ServiceUnavailable, migrationProblem);
            }

            _logger.LogError(ex, "Erreur inattendue lors du chargement des listes pour le tenant {TenantId} (utilisateur {UserId})", tenantId, userId);
            var genericProblem = new ProblemDetails
            {
                Title = ProblemDetailsFrenchLocalizer.ResolveTitle(StatusCodes.Status500InternalServerError),
                Detail = ProblemDetailsFrenchLocalizer.ResolveDetail(StatusCodes.Status500InternalServerError),
                Status = StatusCodes.Status500InternalServerError
            };
            return StatusCode(StatusCodes.Status500InternalServerError, genericProblem);
        }
    }

    /// <summary>
    /// Renomme une liste existante.
    /// </summary>
    /// <param name="listId">Identifiant de la liste.</param>
    /// <param name="request">Nouveau nom.</param>
    /// <param name="ct">Token d'annulation.</param>
    /// <returns>Aucun contenu si la mise à jour réussit.</returns>
    /// <response code="204">Liste renommée.</response>
    /// <response code="400">Nom invalide.</response>
    /// <response code="404">Liste introuvable.</response>
    /// <response code="409">Conflit sur le nom.</response>
    [HttpPut("{listId:guid}")]
    public async Task<IActionResult> Rename(Guid listId, RenameListRequest request, CancellationToken ct)
    {
        if (!TryGetTenantId(out var tenantId, out var problem))
        {
            return BadRequest(problem);
        }

        try
        {
            await _service.RenameListAsync(tenantId, listId, request, ct);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = ProblemDetailsFrenchLocalizer.ResolveTitle(StatusCodes.Status400BadRequest),
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails
            {
                Title = ProblemDetailsFrenchLocalizer.ResolveTitle(StatusCodes.Status409Conflict),
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict
            });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ProblemDetails
            {
                Title = ProblemDetailsFrenchLocalizer.ResolveTitle(StatusCodes.Status404NotFound),
                Detail = "Liste introuvable.",
                Status = StatusCodes.Status404NotFound
            });
        }
    }

    /// <summary>
    /// Supprime une liste et les liaisons prospects associées.
    /// </summary>
    /// <param name="listId">Identifiant de la liste.</param>
    /// <param name="ct">Token d'annulation.</param>
    /// <returns>204 si la suppression est effectuée.</returns>
    /// <response code="204">Liste supprimée.</response>
    /// <response code="404">Liste introuvable.</response>
    [HttpDelete("{listId:guid}")]
    public async Task<IActionResult> Delete(Guid listId, CancellationToken ct)
    {
        if (!TryGetTenantId(out var tenantId, out var problem))
        {
            return BadRequest(problem);
        }

        try
        {
            await _service.DeleteListAsync(tenantId, listId, ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ProblemDetails
            {
                Title = ProblemDetailsFrenchLocalizer.ResolveTitle(StatusCodes.Status404NotFound),
                Detail = "Liste introuvable.",
                Status = StatusCodes.Status404NotFound
            });
        }
    }

    /// <summary>
    /// Ajoute un ensemble de prospects à une liste donnée.
    /// </summary>
    /// <param name="listId">Identifiant de la liste cible.</param>
    /// <param name="request">Identifiants de prospects à ajouter.</param>
    /// <param name="ct">Token d'annulation.</param>
    /// <returns>Nombre d'éléments ajoutés et ignorés.</returns>
    /// <response code="200">Résultat de l'ajout.</response>
    /// <response code="404">Liste introuvable.</response>
    [HttpPost("{listId:guid}/add")]
    public async Task<ActionResult<AddToListResponse>> Add(Guid listId, AddToListRequest request, CancellationToken ct)
    {
        if (!TryGetTenantId(out var tenantId, out var problem))
        {
            return BadRequest(problem);
        }

        var response = await _service.AddToListAsync(tenantId, listId, request, ct);
        return Ok(response);
    }

    /// <summary>
    /// Retourne le détail d'une liste, avec ses prospects scorés.
    /// </summary>
    /// <param name="listId">Identifiant de la liste.</param>
    /// <param name="ct">Token d'annulation.</param>
    /// <returns>Détail de la liste.</returns>
    /// <response code="200">Liste trouvée.</response>
    /// <response code="404">Liste introuvable.</response>
    [HttpGet("{listId:guid}")]
    public async Task<ActionResult<ProspectListResponse>> Get(Guid listId, CancellationToken ct)
    {
        if (!TryGetTenantId(out var tenantId, out var problem))
        {
            return BadRequest(problem);
        }

        try
        {
            var response = await _service.GetListAsync(tenantId, listId, ct);
            return Ok(response);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ProblemDetails
            {
                Title = ProblemDetailsFrenchLocalizer.ResolveTitle(StatusCodes.Status404NotFound),
                Detail = "Liste introuvable.",
                Status = StatusCodes.Status404NotFound
            });
        }
    }

    private bool TryGetTenantId(out Guid tenantId, out ProblemDetails? problem)
    {
        tenantId = _tenantProvider.TenantId;
        if (tenantId != Guid.Empty)
        {
            problem = null;
            return true;
        }

        _logger.LogWarning("Tenant header missing or invalid for user {UserId}", GetUserId());
        problem = new ProblemDetails
        {
            Title = "Tenant manquant",
            Detail = "L'en-tête X-Tenant-Id est manquant ou invalide.",
            Status = StatusCodes.Status400BadRequest
        };
        return false;
    }

    private string? GetUserId()
    {
        return User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User?.FindFirst("sub")?.Value;
    }
}

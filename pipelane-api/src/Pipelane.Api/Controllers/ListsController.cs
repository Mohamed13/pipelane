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

using Pipelane.Application.Hunter;
using Pipelane.Infrastructure.Persistence;

namespace Pipelane.Api.Controllers;

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

    [HttpPost]
    public async Task<ActionResult<object>> Create(CreateListRequest request, CancellationToken ct)
    {
        if (!TryGetTenantId(out var tenantId, out var problem))
        {
            return BadRequest(problem);
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Nom de liste obligatoire.");
        }

    var id = await _service.CreateListAsync(tenantId, request, ct);
    return Ok(new { id });
  }

  [HttpGet]
  public async Task<ActionResult<IReadOnlyList<ProspectListSummary>>> GetAll(CancellationToken ct)
  {
    if (!TryGetTenantId(out var tenantId, out var problem))
    {
      return BadRequest(problem);
    }

    var userId = GetUserId();
    _logger.LogInformation("Fetching lists for tenant {TenantId} user {UserId} using provider {Provider}", tenantId, userId, _dbDiagnostics.ProviderName);

    try
    {
      var response = await _service.GetListsAsync(tenantId, ct) ?? Array.Empty<ProspectListSummary>();
      var items = response
        .Select(list => new ProspectListSummary(list.Id, list.Name, list.Count, list.CreatedAtUtc, list.UpdatedAtUtc))
        .ToList();

      _logger.LogInformation("Fetched {Count} lists for tenant {TenantId}", items.Count, tenantId);
      return Ok(items);
    }
    catch (Exception ex)
    {
      var pending = await _dbDiagnostics.GetPendingMigrationsAsync(ct);
      if (pending.Count > 0)
      {
        _logger.LogError(ex, "Pending migrations detected while fetching lists for tenant {TenantId}: {PendingMigrations}", tenantId, pending);
        var migrationProblem = new ProblemDetails
        {
          Title = "DB_MIGRATION_PENDING",
          Detail = "La base de données nécessite l'application des dernières migrations.",
          Status = StatusCodes.Status500InternalServerError
        };
        return StatusCode(StatusCodes.Status500InternalServerError, migrationProblem);
      }

      _logger.LogError(ex, "Unexpected error while fetching lists for tenant {TenantId} user {UserId}", tenantId, userId);
      var genericProblem = new ProblemDetails
      {
        Title = "lists_fetch_failed",
        Detail = "Erreur inattendue lors de la récupération des listes.",
        Status = StatusCodes.Status500InternalServerError
      };
      return StatusCode(StatusCodes.Status500InternalServerError, genericProblem);
    }
  }

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
            return BadRequest(new ProblemDetails { Title = "invalid_name", Detail = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "name_conflict", Detail = ex.Message });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

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
            return NotFound();
        }
    }

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
            return NotFound();
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
            Title = "tenant_header_missing",
            Detail = "Tenant header (X-Tenant-Id) manquant ou invalide.",
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

using System.Collections.Generic;
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
[Route("api/lists")]
public class ListsController : ControllerBase
{
    private readonly IHunterService _service;
    private readonly ITenantProvider _tenantProvider;

    public ListsController(IHunterService service, ITenantProvider tenantProvider)
    {
        _service = service;
        _tenantProvider = tenantProvider;
    }

    [HttpPost]
    public async Task<ActionResult<object>> Create(CreateListRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Nom de liste obligatoire.");
        }

    var id = await _service.CreateListAsync(_tenantProvider.TenantId, request, ct);
    return Ok(new { id });
  }

  [HttpGet]
  public async Task<ActionResult<IReadOnlyList<ProspectListSummary>>> GetAll(CancellationToken ct)
  {
    var response = await _service.GetListsAsync(_tenantProvider.TenantId, ct);
    return Ok(response);
  }

    [HttpPut("{listId:guid}")]
    public async Task<IActionResult> Rename(Guid listId, RenameListRequest request, CancellationToken ct)
    {
        try
        {
            await _service.RenameListAsync(_tenantProvider.TenantId, listId, request, ct);
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
        try
        {
            await _service.DeleteListAsync(_tenantProvider.TenantId, listId, ct);
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
        var response = await _service.AddToListAsync(_tenantProvider.TenantId, listId, request, ct);
        return Ok(response);
    }

    [HttpGet("{listId:guid}")]
    public async Task<ActionResult<ProspectListResponse>> Get(Guid listId, CancellationToken ct)
    {
        try
        {
            var response = await _service.GetListAsync(_tenantProvider.TenantId, listId, ct);
            return Ok(response);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}

using System.Linq;

using Microsoft.AspNetCore.Mvc;

using Pipelane.Application.Prospecting;
using Pipelane.Infrastructure.Persistence;

namespace Pipelane.Api.Controllers;

[ApiController]
[Microsoft.AspNetCore.Authorization.Authorize]
[Route("api/prospecting/sequences")]
public class ProspectingSequencesController : ControllerBase
{
    private readonly IProspectingService _service;
    private readonly ITenantProvider _tenant;

    public ProspectingSequencesController(IProspectingService service, ITenantProvider tenant)
    {
        _service = service;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var sequences = await _service.GetSequencesAsync(ct);
        return Ok(sequences);
    }

    [HttpPost]
    public async Task<ActionResult<ProspectingSequenceDto>> Create(ProspectingSequenceCreateRequest request, CancellationToken ct)
    {
        var sequence = await _service.CreateSequenceAsync(_tenant.TenantId, request, ct);
        return CreatedAtAction(nameof(GetById), new { id = sequence.Id }, sequence);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProspectingSequenceDto>> GetById(Guid id, CancellationToken ct)
    {
        var sequence = (await _service.GetSequencesAsync(ct)).FirstOrDefault(s => s.Id == id);
        if (sequence is null)
        {
            return NotFound();
        }
        return Ok(sequence);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProspectingSequenceDto>> Update(Guid id, ProspectingSequenceUpdateRequest request, CancellationToken ct)
    {
        var sequence = await _service.UpdateSequenceAsync(id, request, ct);
        return Ok(sequence);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteSequenceAsync(id, ct);
        return NoContent();
    }
}

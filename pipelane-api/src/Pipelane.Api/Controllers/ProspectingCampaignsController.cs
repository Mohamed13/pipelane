using Microsoft.AspNetCore.Mvc;

using Pipelane.Application.Prospecting;
using Pipelane.Infrastructure.Persistence;

namespace Pipelane.Api.Controllers;

[ApiController]
[Microsoft.AspNetCore.Authorization.Authorize]
[Route("api/prospecting/campaigns")]
public class ProspectingCampaignsController : ControllerBase
{
    private readonly IProspectingService _service;
    private readonly ITenantProvider _tenant;

    public ProspectingCampaignsController(IProspectingService service, ITenantProvider tenant)
    {
        _service = service;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var campaigns = await _service.GetCampaignsAsync(ct);
        return Ok(campaigns);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var campaign = await _service.GetCampaignAsync(id, ct);
        if (campaign is null)
        {
            return NotFound();
        }
        return Ok(campaign);
    }

    [HttpPost]
    public async Task<ActionResult<ProspectingCampaignDto>> Create(ProspectingCampaignCreateRequest request, CancellationToken ct)
    {
        var campaign = await _service.CreateCampaignAsync(_tenant.TenantId, request, ct);
        return CreatedAtAction(nameof(Get), new { id = campaign.Id }, campaign);
    }

    [HttpPost("{id:guid}/start")]
    public async Task<ActionResult<ProspectingCampaignDto>> Start(Guid id, CancellationToken ct)
    {
        var campaign = await _service.StartCampaignAsync(id, ct);
        return Ok(campaign);
    }

    [HttpPost("{id:guid}/pause")]
    public async Task<ActionResult<ProspectingCampaignDto>> Pause(Guid id, CancellationToken ct)
    {
        var campaign = await _service.PauseCampaignAsync(id, ct);
        return Ok(campaign);
    }

    [HttpGet("{id:guid}/preview")]
    public async Task<ActionResult<ProspectingCampaignPreview>> Preview(Guid id, CancellationToken ct)
    {
        var preview = await _service.PreviewCampaignAsync(id, ct);
        return Ok(preview);
    }
}

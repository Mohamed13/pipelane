using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Pipelane.Application.DTOs;
using Pipelane.Application.Storage;
using Pipelane.Domain.Entities;
using Pipelane.Infrastructure.Security;

namespace Pipelane.Api.Controllers;

[ApiController]
[Microsoft.AspNetCore.Authorization.Authorize]
[Route("onboarding")] 
public class OnboardingController : ControllerBase
{
    private readonly IAppDbContext _db;
    private readonly IEncryptionService _enc;
    private readonly Pipelane.Infrastructure.Persistence.ITenantProvider _tenant;
    public OnboardingController(IAppDbContext db, IEncryptionService enc, Pipelane.Infrastructure.Persistence.ITenantProvider tenant)
    { _db = db; _enc = enc; _tenant = tenant; }

    [HttpPost("channel-settings")]
    public async Task<IActionResult> SaveChannelSettings([FromBody] ChannelSettingsDto dto, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(dto.Settings);
        var enc = _enc.Encrypt(json);
        _db.ChannelSettings.Add(new ChannelSettings { Id = Guid.NewGuid(), TenantId = _tenant.TenantId, Channel = dto.Channel, SettingsJson = enc });
        await _db.SaveChangesAsync(ct);
        return Ok();
    }
}

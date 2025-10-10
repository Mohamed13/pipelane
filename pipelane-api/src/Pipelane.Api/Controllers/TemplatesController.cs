using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Pipelane.Application.Storage;
using Pipelane.Domain.Entities;

namespace Pipelane.Api.Controllers;

[ApiController]
[Microsoft.AspNetCore.Authorization.Authorize]
[Route("templates")]
public class TemplatesController : ControllerBase
{
    private readonly IAppDbContext _db;
    public TemplatesController(IAppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var list = await _db.Templates.OrderBy(t => t.Name).Take(200).ToListAsync(ct);
        return Ok(list);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        // Placeholder: fetch templates from providers
        await Task.CompletedTask;
        return Ok(new { updated = 0 });
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Pipelane.Application.Storage;
using Pipelane.Domain.Entities;
using Pipelane.Infrastructure.Persistence;
using Pipelane.Infrastructure.Security;

namespace Pipelane.Api.Controllers;

[ApiController]
[Authorize]
[Route("users")]
public class UsersController : ControllerBase
{
    private readonly IAppDbContext _db;
    private readonly ITenantProvider _tenant;
    private readonly IPasswordHasher _hasher;

    public UsersController(IAppDbContext db, ITenantProvider tenant, IPasswordHasher hasher)
    { _db = db; _tenant = tenant; _hasher = hasher; }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var tid = _tenant.TenantId;
        var users = await _db.Users.Where(u => u.TenantId == tid)
            .Select(u => new { u.Id, u.Email, u.Role, u.CreatedAt })
            .ToListAsync(ct);
        return Ok(users);
    }

    public record CreateUserRequest(string Email, string Password, string Role);

    [HttpPost]
    [Authorize(Roles = "owner")]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest req, CancellationToken ct)
    {
        var tid = _tenant.TenantId;
        var exists = await _db.Users.AnyAsync(u => u.TenantId == tid && u.Email == req.Email, ct);
        if (exists) return Conflict(new { message = "Email already exists" });
        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tid,
            Email = req.Email,
            PasswordHash = _hasher.Hash(req.Password),
            Role = string.IsNullOrWhiteSpace(req.Role) ? "member" : req.Role,
            CreatedAt = DateTime.UtcNow
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        return Created($"/users/{user.Id}", new { user.Id, user.Email, user.Role });
    }
}


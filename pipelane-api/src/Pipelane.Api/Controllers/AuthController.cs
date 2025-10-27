using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

using Pipelane.Domain.Entities;
using Pipelane.Infrastructure.Persistence;
using Pipelane.Infrastructure.Security;

namespace Pipelane.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IConfiguration _config;

    public AuthController(AppDbContext db, IPasswordHasher hasher, IConfiguration config)
    {
        _db = db; _hasher = hasher; _config = config;
    }

    public record LoginRequest(string Email, string Password, Guid? TenantId);
    public record RegisterRequest(string Email, string Password, string? TenantName);

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrEmpty(req.Password))
            return Unauthorized();

        // Query without tenant filter to locate the user by email (or specific tenant if provided)
        var query = _db.Users.AsNoTracking().IgnoreQueryFilters().AsQueryable();
        if (req.TenantId is Guid tid && tid != Guid.Empty)
            query = query.Where(u => u.TenantId == tid);

        var user = await query.FirstOrDefaultAsync(u => u.Email == req.Email, ct);
        if (user is null) return Unauthorized();
        if (!_hasher.Verify(req.Password, user.PasswordHash)) return Unauthorized();

        var token = CreateJwt(user);
        return Ok(new { token, tenantId = user.TenantId, role = user.Role });
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest();

        // Create a new tenant and owner user
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = string.IsNullOrWhiteSpace(req.TenantName) ? "Tenant" : req.TenantName! };
        var exists = await _db.Users.AsNoTracking().IgnoreQueryFilters().AnyAsync(u => u.Email == req.Email, ct);
        if (exists) return Conflict(new { message = "Email already exists" });

        _db.Tenants.Add(tenant);
        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Email = req.Email,
            PasswordHash = _hasher.Hash(req.Password),
            Role = "owner",
            CreatedAt = DateTime.UtcNow
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        var token = CreateJwt(user);
        return Ok(new { token, tenantId = user.TenantId, role = user.Role });
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        var claims = User.Claims.ToDictionary(c => c.Type, c => c.Value);
        return Ok(new { sub = claims.GetValueOrDefault(JwtRegisteredClaimNames.Sub), email = claims.GetValueOrDefault(JwtRegisteredClaimNames.Email), tid = claims.GetValueOrDefault("tid"), role = claims.GetValueOrDefault(ClaimTypes.Role) });
    }

    private string CreateJwt(User user)
    {
        var key = _config["JWT_KEY"] ?? "dev-secret-key-please-change";
        var keyBytes = Encoding.UTF8.GetBytes(key);
        if (keyBytes.Length < 32) keyBytes = SHA256.HashData(keyBytes);
        var signingKey = new SymmetricSecurityKey(keyBytes);
        var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("tid", user.TenantId.ToString()),
            new("tenant_ids", user.TenantId.ToString()),
            new(ClaimTypes.Role, user.Role)
        };

        var jwt = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds
        );
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }
}

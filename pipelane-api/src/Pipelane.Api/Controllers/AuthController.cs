using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace Pipelane.Api.Controllers;

public record LoginRequest(string Email, string Password, Guid? TenantId);
public record LoginResponse(string Token, Guid TenantId, string Role);

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _cfg;
    public AuthController(IConfiguration cfg) => _cfg = cfg;

    [AllowAnonymous]
    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest req)
    {
        // Demo in-memory auth: any email/password accepted, role=owner; tenant from request or new
        var tenantId = req.TenantId ?? Guid.NewGuid();
        var key = _cfg["JWT_KEY"] ?? Environment.GetEnvironmentVariable("JWT_KEY") ?? "dev-secret-key-please-change";
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, req.Email),
            new Claim("tid", tenantId.ToString()),
            new Claim(ClaimTypes.Role, "owner")
        };
        var token = new JwtSecurityToken(
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256)
        );
        var jwt = new JwtSecurityTokenHandler().WriteToken(token);
        return Ok(new LoginResponse(jwt, tenantId, "owner"));
    }
}


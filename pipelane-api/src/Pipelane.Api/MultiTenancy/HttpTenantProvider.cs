using System.Security.Claims;

using Microsoft.AspNetCore.Http;

using Pipelane.Infrastructure.Persistence;

namespace Pipelane.Api.MultiTenancy;

public sealed class HttpTenantProvider : ITenantProvider
{
    private const string TenantHeader = "X-Tenant-Id";
    private const string TenantIdClaim = "tid";
    private const string TenantIdsClaim = "tenant_ids";

    private readonly IHttpContextAccessor _http;
    public HttpTenantProvider(IHttpContextAccessor http) => _http = http;

    /// <inheritdoc/>
    public Guid TenantId
    {
        get
        {
            var ctx = _http.HttpContext;
            if (ctx is null)
            {
                return Guid.Empty; // background/no context: no filter
            }

            if (ctx.Request.Headers.TryGetValue(TenantHeader, out var headerValues) &&
                Guid.TryParse(headerValues.FirstOrDefault(), out var fromHeader))
            {
                return fromHeader;
            }

            var tenantFromTid = ResolveClaim(ctx.User, TenantIdClaim);
            if (tenantFromTid.HasValue)
            {
                return tenantFromTid.Value;
            }

            var tenantIds = ctx.User?.FindAll(TenantIdsClaim)
                .Select(claim => claim.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (tenantIds is { Count: 1 } && Guid.TryParse(tenantIds[0], out var singleTenant))
            {
                return singleTenant;
            }

            return Guid.Empty;
        }
    }

    private static Guid? ResolveClaim(ClaimsPrincipal? user, string claimType)
    {
        if (user is null)
        {
            return null;
        }

        var value = user.FindFirst(claimType)?.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Guid.TryParse(value, out var id) ? id : null;
    }
}

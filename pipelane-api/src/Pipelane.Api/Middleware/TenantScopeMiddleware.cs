using System.Linq;
using System.Security.Claims;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Pipelane.Api.Middleware;

public sealed class TenantScopeMiddleware
{
    private const string TenantIdsClaim = "tenant_ids";
    private const string TenantHeader = "X-Tenant-Id";

    private readonly RequestDelegate _next;

    public TenantScopeMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var allowedTenants = context.User.FindAll(TenantIdsClaim)
                .Select(claim => claim.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (allowedTenants.Count > 0 &&
                context.Request.Headers.TryGetValue(TenantHeader, out var headerValue) &&
                !string.IsNullOrWhiteSpace(headerValue))
            {
                if (!Guid.TryParse(headerValue.ToString(), out var requestedTenant) ||
                    !allowedTenants.Contains(requestedTenant.ToString()))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new ProblemDetails
                    {
                        Title = "tenant_forbidden",
                        Detail = "The provided tenant is not allowed by this token.",
                        Status = StatusCodes.Status403Forbidden
                    });
                    return;
                }
            }
        }

        await _next(context);
    }
}

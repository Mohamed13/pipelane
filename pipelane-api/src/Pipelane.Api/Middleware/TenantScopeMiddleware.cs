using System.Linq;
using System.Security.Claims;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Pipelane.Api.Middleware;

/// <summary>
/// Valide que le tenant demandé via X-Tenant-Id est autorisé pour l'utilisateur courant et peuple les erreurs FR.
/// </summary>
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
                    var correlationId = context.Items.TryGetValue("CorrelationId", out var rawCorrelation)
                        ? rawCorrelation?.ToString()
                        : null;
                    correlationId ??= context.TraceIdentifier;
                    await context.Response.WriteAsJsonAsync(new ProblemDetails
                    {
                        Title = "Accès refusé",
                        Detail = "Ce jeton ne permet pas d'accéder à ce tenant.",
                        Status = StatusCodes.Status403Forbidden,
                        Instance = context.Request.Path,
                        Extensions = { ["traceId"] = context.TraceIdentifier, ["correlationId"] = correlationId! }
                    });
                    return;
                }
            }
        }

        await _next(context);
    }
}

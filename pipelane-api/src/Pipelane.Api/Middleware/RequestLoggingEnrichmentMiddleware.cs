using System;
using System.Security.Claims;

using Microsoft.AspNetCore.Http;

using Serilog.Context;

namespace Pipelane.Api.Middleware;

/// <summary>
/// Ajoute les métadonnées clés (corrélation, tenant, utilisateur, provider, message) au contexte Serilog.
/// </summary>
public sealed class RequestLoggingEnrichmentMiddleware
{
    private const string CorrelationHeader = "X-Correlation-Id";
    private const string TenantHeader = "X-Tenant-Id";
    private const string ProviderHeader = "X-Provider";
    private const string MessageHeader = "X-Message-Id";

    private readonly RequestDelegate _next;

    public RequestLoggingEnrichmentMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = EnsureCorrelationId(context);
        context.Items["CorrelationId"] = correlationId;

        var tenantId = context.Request.Headers.TryGetValue(TenantHeader, out var tenant)
            ? tenant.ToString()
            : "tenant_non_renseigne";
        var provider = context.Request.Headers.TryGetValue(ProviderHeader, out var providerHeader)
            ? providerHeader.ToString()
            : "provider_inconnu";
        var messageId = context.Request.Headers.TryGetValue(MessageHeader, out var messageHeader)
            ? messageHeader.ToString()
            : "message_inconnu";
        var userId = ResolveUserId(context.User);

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("TenantId", tenantId))
        using (LogContext.PushProperty("UserId", userId))
        using (LogContext.PushProperty("Provider", provider))
        using (LogContext.PushProperty("MessageId", messageId))
        {
            await _next(context);
        }
    }

    private static string ResolveUserId(ClaimsPrincipal? user)
    {
        return user?.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? user?.FindFirst("sub")?.Value
               ?? "utilisateur_inconnu";
    }

    private static string EnsureCorrelationId(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(CorrelationHeader, out var header)
            || string.IsNullOrWhiteSpace(header))
        {
            var generated = Guid.NewGuid().ToString("N");
            context.Request.Headers[CorrelationHeader] = generated;
            context.Response.Headers[CorrelationHeader] = generated;
            return generated;
        }

        var value = header.ToString();
        context.Response.Headers[CorrelationHeader] = value;
        return value;
    }
}

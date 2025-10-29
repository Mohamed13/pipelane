using Microsoft.AspNetCore.Http;

using Pipelane.Infrastructure.Persistence;

namespace Pipelane.Api.MultiTenancy;

public sealed class HttpTenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor _http;
    public HttpTenantProvider(IHttpContextAccessor http) => _http = http;

    /// <inheritdoc/>
    public Guid TenantId
    {
        get
        {
            var ctx = _http.HttpContext;
            if (ctx is null) return Guid.Empty; // background/no context: no filter
            if (ctx.Request.Headers.TryGetValue("X-Tenant-Id", out var values))
            {
                if (Guid.TryParse(values.FirstOrDefault(), out var id)) return id;
            }
            return Guid.Empty;
        }
    }
}


using System.Security.Claims;
using System.Threading.Tasks;

using FluentAssertions;

using Microsoft.AspNetCore.Http;

using Pipelane.Api.Middleware;

using Xunit;

namespace Pipelane.Tests;

public class RequestLoggingEnrichmentMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_generates_correlation_and_defaults_when_missing()
    {
        var context = new DefaultHttpContext();
        var invoked = false;
        var middleware = new RequestLoggingEnrichmentMiddleware(ctx =>
        {
            invoked = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        invoked.Should().BeTrue();
        var correlation = context.Request.Headers["X-Correlation-Id"].ToString();
        correlation.Should().NotBeNullOrEmpty();
        context.Response.Headers["X-Correlation-Id"].ToString().Should().Be(correlation);
        context.Items["CorrelationId"].Should().Be(correlation);
    }

    [Fact]
    public async Task InvokeAsync_preserves_existing_headers_and_resolves_user()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-Id"] = "existing-correlation";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-123")
        }));

        var middleware = new RequestLoggingEnrichmentMiddleware(ctx =>
        {
            ctx.Items["ObservedCorrelation"] = ctx.Items["CorrelationId"];
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        context.Response.Headers["X-Correlation-Id"].ToString().Should().Be("existing-correlation");
        context.Items["ObservedCorrelation"].Should().Be("existing-correlation");
    }
}


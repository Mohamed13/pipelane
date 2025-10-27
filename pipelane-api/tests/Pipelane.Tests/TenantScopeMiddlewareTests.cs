using System;
using System.Security.Claims;
using System.Threading.Tasks;

using FluentAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Pipelane.Api.Middleware;

using Xunit;

namespace Pipelane.Tests;

public class TenantScopeMiddlewareTests
{
    [Fact]
    public async Task Should_Forbid_When_Tenant_Not_In_Claim()
    {
        var invoked = false;
        var middleware = new TenantScopeMiddleware(_ =>
        {
            invoked = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        var tenantAllowed = Guid.NewGuid();
        var tenantHeader = Guid.NewGuid();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user"),
            new Claim("tenant_ids", tenantAllowed.ToString())
        }, "test"));
        context.Request.Headers["X-Tenant-Id"] = tenantHeader.ToString();

        await middleware.InvokeAsync(context);

        invoked.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Should_Pass_When_Tenant_In_Claim()
    {
        var invoked = false;
        var middleware = new TenantScopeMiddleware(_ =>
        {
            invoked = true;
            return Task.CompletedTask;
        });

        var tenant = Guid.NewGuid();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "user"),
                new Claim("tenant_ids", tenant.ToString())
            }, "test"))
        };
        context.Request.Headers["X-Tenant-Id"] = tenant.ToString();

        await middleware.InvokeAsync(context);

        invoked.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }
}

using System;
using System.Security.Claims;

using FluentAssertions;

using Microsoft.AspNetCore.Http;

using Pipelane.Api.MultiTenancy;

using Xunit;

namespace Pipelane.Tests;

public sealed class HttpTenantProviderTests
{
    [Fact]
    public void TenantId_Uses_Header_When_Available()
    {
        var expected = Guid.NewGuid();
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Tenant-Id"] = expected.ToString();
        var accessor = new HttpContextAccessor { HttpContext = context };

        var provider = new HttpTenantProvider(accessor);

        provider.TenantId.Should().Be(expected);
    }

    [Fact]
    public void TenantId_Falls_Back_To_Tid_Claim()
    {
        var expected = Guid.NewGuid();
        var identity = new ClaimsIdentity(new[] { new Claim("tid", expected.ToString()) }, "test");
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        var accessor = new HttpContextAccessor { HttpContext = context };

        var provider = new HttpTenantProvider(accessor);

        provider.TenantId.Should().Be(expected);
    }

    [Fact]
    public void TenantId_Uses_Single_TenantIds_Claim_When_Header_And_Tid_Missing()
    {
        var expected = Guid.NewGuid();
        var identity = new ClaimsIdentity(new[] { new Claim("tenant_ids", expected.ToString()) }, "test");
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        var accessor = new HttpContextAccessor { HttpContext = context };

        var provider = new HttpTenantProvider(accessor);

        provider.TenantId.Should().Be(expected);
    }

    [Fact]
    public void TenantId_Returns_Empty_When_Multiple_TenantIds_Without_Header()
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim("tenant_ids", Guid.NewGuid().ToString()),
                new Claim("tenant_ids", Guid.NewGuid().ToString())
            },
            "test");
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        var accessor = new HttpContextAccessor { HttpContext = context };

        var provider = new HttpTenantProvider(accessor);

        provider.TenantId.Should().Be(Guid.Empty);
    }
}

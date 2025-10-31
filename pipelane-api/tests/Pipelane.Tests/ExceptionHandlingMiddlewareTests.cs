using System;
using System.Threading.Tasks;

using FluentAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

using Pipelane.Api.Middleware;

using Xunit;

namespace Pipelane.Tests;

public class ExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_writes_problem_details_for_known_exception()
    {
        var service = new CapturingProblemDetailsService();
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new InvalidOperationException("conflict detected"),
            NullLogger<ExceptionHandlingMiddleware>.Instance,
            service);
        var context = new DefaultHttpContext();
        context.TraceIdentifier = "trace-123";
        context.Items["CorrelationId"] = "corr-456";

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        service.Captured.Should().NotBeNull();
        var problem = service.Captured!.ProblemDetails;
        problem.Status.Should().Be(StatusCodes.Status409Conflict);
        problem.Title.Should().Be("Conflit métier");
        problem.Detail.Should().Be("conflict detected");
        problem.Instance.Should().Be(context.Request.Path);
        problem.Extensions["traceId"].Should().Be("trace-123");
        problem.Extensions["correlationId"].Should().Be("corr-456");
    }

    private sealed class CapturingProblemDetailsService : IProblemDetailsService
    {
        public ProblemDetailsContext? Captured { get; private set; }

        public ValueTask WriteAsync(ProblemDetailsContext context)
        {
            Captured = context;
            context.HttpContext.Response.ContentType = "application/problem+json";
            return ValueTask.CompletedTask;
        }
    }
}

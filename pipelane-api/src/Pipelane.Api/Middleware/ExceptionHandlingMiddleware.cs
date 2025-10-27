using System;
using System.Collections.Generic;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Pipelane.Api.Middleware;

/// <summary>
/// Capture les exceptions non gérées et retourne une réponse ProblemDetails cohérente en français.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IProblemDetailsService _problemDetailsService;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IProblemDetailsService problemDetailsService)
    {
        _next = next;
        _logger = logger;
        _problemDetailsService = problemDetailsService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            if (context.Response.HasStarted)
            {
                _logger.LogError(ex, "Erreur non gérée après le démarrage de la réponse (trace {TraceId})", context.TraceIdentifier);
                throw;
            }

            context.Response.Clear();
            var problem = MapException(ex, context);
            context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;

            var pdContext = new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails = problem
            };

            _logger.LogError(
                ex,
                "Erreur {ProblemCode} sur {Method} {Path} (corrélation {CorrelationId})",
                problem.Title,
                context.Request.Method,
                context.Request.Path,
                problem.Extensions.TryGetValue("correlationId", out var correlation) ? correlation : context.TraceIdentifier);

            await _problemDetailsService.WriteAsync(pdContext);
        }
    }

    private static ProblemDetails MapException(Exception exception, HttpContext context)
    {
        var statusCode = exception switch
        {
            ArgumentNullException => StatusCodes.Status400BadRequest,
            ArgumentException => StatusCodes.Status400BadRequest,
            InvalidOperationException => StatusCodes.Status409Conflict,
            KeyNotFoundException => StatusCodes.Status404NotFound,
            UnauthorizedAccessException => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError
        };

        var title = ProblemDetailsFrenchLocalizer.ResolveTitle(statusCode);
        var detail = !string.IsNullOrWhiteSpace(exception.Message)
            ? exception.Message
            : ProblemDetailsFrenchLocalizer.ResolveDetail(statusCode);

        var correlationId = context.Items.TryGetValue("CorrelationId", out var rawCorrelation)
            ? rawCorrelation?.ToString()
            : null;

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };

        problem.Extensions["traceId"] = context.TraceIdentifier;
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            problem.Extensions["correlationId"] = correlationId!;
        }

        return problem;
    }
}

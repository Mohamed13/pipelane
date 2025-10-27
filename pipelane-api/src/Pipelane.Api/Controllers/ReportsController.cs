using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using Pipelane.Application.Services;
using Pipelane.Infrastructure.Persistence;

namespace Pipelane.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/reports")]
public sealed class ReportsController : ControllerBase
{
    private readonly IReportService _reports;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<ReportsController> _logger;

    public ReportsController(
        IReportService reports,
        ITenantProvider tenantProvider,
        ILogger<ReportsController> logger)
    {
        _reports = reports;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<ReportSummary>> Summary([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var tenantId = _tenantProvider.TenantId;
        if (tenantId == Guid.Empty)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "tenant_missing",
                Detail = "Provide X-Tenant-Id header to fetch reports."
            });
        }

        var (start, end) = ResolveRange(from, to);
        var summary = await _reports.GetSummaryAsync(tenantId, start, end, ct).ConfigureAwait(false);
        _logger.LogInformation("Report summary requested tenant={TenantId} from={From} to={To}", tenantId, summary.From, summary.To);
        return Ok(summary);
    }

    [HttpGet("summary.pdf")]
    public async Task<IActionResult> SummaryPdf([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var tenantId = _tenantProvider.TenantId;
        if (tenantId == Guid.Empty)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "tenant_missing",
                Detail = "Provide X-Tenant-Id header to fetch reports."
            });
        }

        var (start, end) = ResolveRange(from, to);
        var payload = await _reports.RenderSummaryPdfAsync(tenantId, start, end, ct).ConfigureAwait(false);
        var filename = $"pipelane-summary-{start:yyyyMMdd}-{end:yyyyMMdd}.pdf";
        return File(payload, "application/pdf", filename);
    }

    private static (DateTime Start, DateTime End) ResolveRange(DateTime? from, DateTime? to)
    {
        var end = to ?? DateTime.UtcNow;
        var start = from ?? end.AddDays(-7);
        if (end < start)
        {
            (start, end) = (end, start);
        }
        return (start, end);
    }
}

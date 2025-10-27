using Pipelane.Application.Analytics;

namespace Pipelane.Application.Services;

public interface IReportService
{
    Task<ReportSummary> GetSummaryAsync(Guid tenantId, DateTime from, DateTime to, CancellationToken ct);
    Task<byte[]> RenderSummaryPdfAsync(Guid tenantId, DateTime from, DateTime to, CancellationToken ct);
}

public sealed record ReportSummary(
    DateTime From,
    DateTime To,
    DeliveryTotals Totals,
    IReadOnlyCollection<DeliveryChannelBreakdown> ByChannel,
    IReadOnlyCollection<DeliveryTemplateBreakdown> TopTemplates,
    int MeetingsBooked);

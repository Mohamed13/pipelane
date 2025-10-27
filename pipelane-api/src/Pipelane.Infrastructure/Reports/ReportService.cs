using System.Globalization;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Pipelane.Application.Analytics;
using Pipelane.Application.Services;
using Pipelane.Application.Storage;
using Pipelane.Domain.Enums;
using Pipelane.Domain.Enums.Prospecting;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Pipelane.Infrastructure.Reports;

public sealed class ReportService : IReportService
{
    private readonly IAnalyticsService _analytics;
    private readonly IAppDbContext _db;
    private readonly TimeProvider _clock;
    private readonly ILogger<ReportService> _logger;

    static ReportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public ReportService(
        IAnalyticsService analytics,
        IAppDbContext db,
        TimeProvider? clock,
        ILogger<ReportService> logger)
    {
        _analytics = analytics;
        _db = db;
        _clock = clock ?? TimeProvider.System;
        _logger = logger;
    }

    public async Task<ReportSummary> GetSummaryAsync(Guid tenantId, DateTime from, DateTime to, CancellationToken ct)
    {
        var (start, end) = NormalizeRange(from, to);

        var analytics = await _analytics.GetDeliveryAsync(start, end, ct).ConfigureAwait(false);
        var meetings = await _db.ProspectReplies
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId
                        && r.ReceivedAtUtc >= start
                        && r.ReceivedAtUtc <= end
                        && (r.Intent == ReplyIntent.Interested || r.Intent == ReplyIntent.MeetingRequested))
            .CountAsync(ct)
            .ConfigureAwait(false);

        var channels = analytics.ByChannel
            .OrderByDescending(c => c.Sent)
            .ToList();

        var topTemplates = analytics.ByTemplate
            .OrderByDescending(t => t.Opened)
            .ThenByDescending(t => t.Sent)
            .Take(5)
            .ToList();

        return new ReportSummary(start, end, analytics.Totals, channels, topTemplates, meetings);
    }

    public async Task<byte[]> RenderSummaryPdfAsync(Guid tenantId, DateTime from, DateTime to, CancellationToken ct)
    {
        var summary = await GetSummaryAsync(tenantId, from, to, ct).ConfigureAwait(false);
        var generatedAt = _clock.GetUtcNow().UtcDateTime;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(36);
                page.Size(PageSizes.A4);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(TextStyle.Default.FontSize(11));

                page.Header().Column(header =>
                {
                    header.Spacing(4);
                    header.Item().Text("Pipelane Delivery Summary").FontSize(20).SemiBold().FontColor("#0EA5E9");
                    header.Item().Text($"{summary.From:yyyy-MM-dd} → {summary.To:yyyy-MM-dd}")
                        .FontColor("#475569");
                });

                page.Content().Column(column =>
                {
                    column.Spacing(18);

                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Element(container => Kpi(container, "Sent", summary.Totals.Sent));
                        row.RelativeItem().Element(container => Kpi(container, "Delivered", summary.Totals.Delivered));
                        row.RelativeItem().Element(container => Kpi(container, "Opened", summary.Totals.Opened));
                        row.RelativeItem().Element(container => Kpi(container, "Failed", summary.Totals.Failed + summary.Totals.Bounced));
                        row.RelativeItem().Element(container => Kpi(container, "Meetings booked", summary.MeetingsBooked));
                    });

                    column.Item().Element(container =>
                    {
                        if (summary.ByChannel.Count == 0)
                        {
                            container.Text("No channel activity recorded for this period.")
                                .FontColor("#64748B");
                            return;
                        }

                        container.Column(tableColumn =>
                        {
                            tableColumn.Spacing(8);
                            tableColumn.Item().Text("Channels").FontSize(14).SemiBold();
                            tableColumn.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn();
                                    columns.RelativeColumn();
                                    columns.RelativeColumn();
                                    columns.RelativeColumn();
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(TableHeader).Text("Channel");
                                    header.Cell().Element(TableHeader).Text("Sent");
                                    header.Cell().Element(TableHeader).Text("Delivered");
                                    header.Cell().Element(TableHeader).Text("Opened");
                                });

                                foreach (var channel in summary.ByChannel)
                                {
                                    table.Cell().Element(TableCell).Text(channel.Channel.ToUpperInvariant());
                                    table.Cell().Element(TableCell).Text(FormatNumber(channel.Sent));
                                    table.Cell().Element(TableCell).Text(FormatNumber(channel.Delivered));
                                    table.Cell().Element(TableCell).Text(FormatNumber(channel.Opened));
                                }
                            });
                        });
                    });

                    column.Item().Element(container =>
                    {
                        if (summary.TopTemplates.Count == 0)
                        {
                            container.Text("Templates: no usage recorded during this period.")
                                .FontColor("#64748B");
                            return;
                        }

                        container.Column(tableColumn =>
                        {
                            tableColumn.Spacing(8);
                            tableColumn.Item().Text("Top templates").FontSize(14).SemiBold();
                            tableColumn.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn();
                                    columns.RelativeColumn();
                                    columns.RelativeColumn();
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(TableHeader).Text("Template");
                                    header.Cell().Element(TableHeader).Text("Channel");
                                    header.Cell().Element(TableHeader).Text("Sent");
                                    header.Cell().Element(TableHeader).Text("Opened");
                                });

                                foreach (var template in summary.TopTemplates)
                                {
                                    table.Cell().Element(TableCell).Text(template.Template);
                                    table.Cell().Element(TableCell).Text(template.Channel.ToUpperInvariant());
                                    table.Cell().Element(TableCell).Text(FormatNumber(template.Sent));
                                    table.Cell().Element(TableCell).Text(FormatNumber(template.Opened));
                                }
                            });
                        });
                    });
                });

                page.Footer().AlignRight().Text(
                        $"Generated {generatedAt:yyyy-MM-dd HH:mm} UTC")
                    .FontColor("#94A3B8");
            });
        });

        using var stream = new MemoryStream();
        document.GeneratePdf(stream);
        _logger.LogInformation("Report PDF generated tenant={TenantId} from={From} to={To}", tenantId, summary.From, summary.To);
        return stream.ToArray();
    }

    private static (DateTime From, DateTime To) NormalizeRange(DateTime from, DateTime to)
    {
        if (to < from)
        {
            return (to, from);
        }

        return (from, to);
    }

    private static string FormatNumber(int value)
        => value.ToString("N0", CultureInfo.InvariantCulture);

    private static IContainer Kpi(IContainer container, string label, int value)
    {
        var panel = container.Padding(12).Background("#F1F5F9").Border(1);
        panel.BorderColor("#CBD5F5");
        panel.Column(column =>
        {
            column.Spacing(4);
            column.Item().Text(label).FontSize(10).FontColor("#475569");
            column.Item().Text(FormatNumber(value)).FontSize(16).SemiBold().FontColor("#0F172A");
        });
        return panel;
    }

    private static IContainer TableHeader(IContainer container)
    {
        var styled = container.Background("#0EA5E9").Padding(6);
        styled.DefaultTextStyle(TextStyle.Default.FontColor(Colors.White).SemiBold());
        return styled;
    }

    private static IContainer TableCell(IContainer container)
    {
        var padded = container.Padding(6);
        var bordered = padded.BorderBottom(0.5f);
        bordered.BorderColor("#E2E8F0");
        return bordered;
    }
}

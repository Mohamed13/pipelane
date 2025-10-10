using Microsoft.EntityFrameworkCore;

using Pipelane.Application.Analytics;
using Pipelane.Application.Storage;
using Pipelane.Domain.Enums;

namespace Pipelane.Application.Services;

public interface IAnalyticsService
{
    Task<DeliveryAnalyticsResult> GetDeliveryAsync(DateTime from, DateTime to, CancellationToken cancellationToken);
}

public sealed class AnalyticsService : IAnalyticsService
{
    private readonly IAppDbContext _db;

    public AnalyticsService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<DeliveryAnalyticsResult> GetDeliveryAsync(DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        if (to < from)
        {
            (from, to) = (to, from);
        }

        var query = _db.Messages
            .AsNoTracking()
            .Where(m => m.CreatedAt >= from && m.CreatedAt <= to);

        var totalsRow = await query
            .GroupBy(_ => 1)
            .Select(g => new DeliveryTotalsRow
            {
                Queued = g.Count(m => m.Status == MessageStatus.Queued),
                Sent = g.Count(m => m.Status == MessageStatus.Sent),
                Delivered = g.Count(m => m.Status == MessageStatus.Delivered),
                Opened = g.Count(m => m.Status == MessageStatus.Opened),
                Failed = g.Count(m => m.Status == MessageStatus.Failed),
                Bounced = g.Count(m => m.Status == MessageStatus.Bounced)
            })
            .FirstOrDefaultAsync(cancellationToken) ?? new DeliveryTotalsRow();

        var channelRows = await query
            .GroupBy(m => m.Channel)
            .Select(g => new DeliveryChannelRow
            {
                Channel = g.Key,
                Queued = g.Count(m => m.Status == MessageStatus.Queued),
                Sent = g.Count(m => m.Status == MessageStatus.Sent),
                Delivered = g.Count(m => m.Status == MessageStatus.Delivered),
                Opened = g.Count(m => m.Status == MessageStatus.Opened),
                Failed = g.Count(m => m.Status == MessageStatus.Failed),
                Bounced = g.Count(m => m.Status == MessageStatus.Bounced)
            })
            .ToListAsync(cancellationToken);

        var templateRows = await query
            .Where(m => m.TemplateName != null)
            .GroupBy(m => new { m.TemplateName, m.Channel })
            .Select(g => new DeliveryTemplateRow
            {
                Template = g.Key.TemplateName!,
                Channel = g.Key.Channel,
                Queued = g.Count(m => m.Status == MessageStatus.Queued),
                Sent = g.Count(m => m.Status == MessageStatus.Sent),
                Delivered = g.Count(m => m.Status == MessageStatus.Delivered),
                Opened = g.Count(m => m.Status == MessageStatus.Opened),
                Failed = g.Count(m => m.Status == MessageStatus.Failed),
                Bounced = g.Count(m => m.Status == MessageStatus.Bounced)
            })
            .ToListAsync(cancellationToken);

        var totals = new DeliveryTotals(
            totalsRow.Queued,
            totalsRow.Sent,
            totalsRow.Delivered,
            totalsRow.Opened,
            totalsRow.Failed,
            totalsRow.Bounced);

        var byChannel = channelRows
            .Select(r => new DeliveryChannelBreakdown(
                r.Channel.ToString().ToLowerInvariant(),
                r.Queued,
                r.Sent,
                r.Delivered,
                r.Opened,
                r.Failed,
                r.Bounced))
            .OrderBy(r => r.Channel)
            .ToList();

        var byTemplate = templateRows
            .Select(r => new DeliveryTemplateBreakdown(
                r.Template,
                r.Channel.ToString().ToLowerInvariant(),
                r.Queued,
                r.Sent,
                r.Delivered,
                r.Opened,
                r.Failed,
                r.Bounced))
            .OrderBy(r => r.Template)
            .ThenBy(r => r.Channel)
            .ToList();

        return new DeliveryAnalyticsResult(totals, byChannel, byTemplate);
    }

    private sealed class DeliveryTotalsRow
    {
        public int Queued { get; init; }
        public int Sent { get; init; }
        public int Delivered { get; init; }
        public int Opened { get; init; }
        public int Failed { get; init; }
        public int Bounced { get; init; }
    }

    private sealed class DeliveryChannelRow
    {
        public Channel Channel { get; init; }
        public int Queued { get; init; }
        public int Sent { get; init; }
        public int Delivered { get; init; }
        public int Opened { get; init; }
        public int Failed { get; init; }
        public int Bounced { get; init; }
    }

    private sealed class DeliveryTemplateRow
    {
        public string Template { get; init; } = string.Empty;
        public Channel Channel { get; init; }
        public int Queued { get; init; }
        public int Sent { get; init; }
        public int Delivered { get; init; }
        public int Opened { get; init; }
        public int Failed { get; init; }
        public int Bounced { get; init; }
    }
}

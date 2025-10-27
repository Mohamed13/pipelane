using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Pipelane.Application.Analytics;
using Pipelane.Application.Storage;
using Pipelane.Domain.Enums;

namespace Pipelane.Application.Services;

public interface IAnalyticsService
{
    Task<DeliveryAnalyticsResult> GetDeliveryAsync(DateTime from, DateTime to, CancellationToken cancellationToken);
    Task<TopMessagesResult> GetTopMessagesAsync(DateTime from, DateTime to, CancellationToken cancellationToken);
}

public sealed class AnalyticsService : IAnalyticsService
{
    private static readonly DeliveryTimelinePoint ZeroTimelinePoint = new(DateTime.MinValue, 0, 0, 0, 0, 0, 0);

    private readonly IAppDbContext _db;

    public AnalyticsService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<DeliveryAnalyticsResult> GetDeliveryAsync(DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        NormalizeRange(ref from, ref to);

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

        var timelineRows = await query
            .GroupBy(m => m.CreatedAt.Date)
            .Select(g => new DeliveryTimelineRow
            {
                Date = g.Key,
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

        var startDate = from.Date;
        var endDate = to.Date;
        var timelineLookup = timelineRows.ToDictionary(r => r.Date, r => r);
        var timeline = new List<DeliveryTimelinePoint>();
        for (var current = startDate; current <= endDate; current = current.AddDays(1))
        {
            var row = timelineLookup.TryGetValue(current, out var data)
                ? data
                : null;

            if (row is null)
            {
                timeline.Add(ZeroTimelinePoint with { Date = current });
            }
            else
            {
                timeline.Add(new DeliveryTimelinePoint(
                    current,
                    row.Queued,
                    row.Sent,
                    row.Delivered,
                    row.Opened,
                    row.Failed,
                    row.Bounced));
            }
        }

        return new DeliveryAnalyticsResult(totals, byChannel, byTemplate, timeline);
    }

    public async Task<TopMessagesResult> GetTopMessagesAsync(DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        NormalizeRange(ref from, ref to);

        var messages = await _db.Messages
            .AsNoTracking()
            .Where(m => m.CreatedAt >= from && m.CreatedAt <= to)
            .Select(m => new
            {
                m.Id,
                m.ConversationId,
                m.Channel,
                m.Direction,
                m.TemplateName,
                m.PayloadJson,
                m.Status,
                m.CreatedAt,
                m.DeliveredAt,
                m.OpenedAt,
                m.FailedAt
            })
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
        {
            return new TopMessagesResult(from, to, Array.Empty<TopMessageItem>(), Array.Empty<TopMessageItem>());
        }

        var groupedByConversation = messages
            .GroupBy(m => m.ConversationId)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToList());

        var aggregates = new Dictionary<string, TopMessageAggregate>(StringComparer.OrdinalIgnoreCase);

        foreach (var (_, conversationMessages) in groupedByConversation)
        {
            TopMessageKey? lastKey = null;
            foreach (var message in conversationMessages)
            {
                if (message.Direction == MessageDirection.Out)
                {
                    var key = ResolveKey(message);
                    if (key is null)
                    {
                        lastKey = null;
                        continue;
                    }

                    var aggregate = aggregates.TryGetValue(key.FullKey, out var existing)
                        ? existing
                        : aggregates[key.FullKey] = new TopMessageAggregate(key);

                    aggregate.TotalSent++;
                    if (message.Status == MessageStatus.Sent)
                    {
                        aggregate.Sent++;
                    }
                    if (message.Status == MessageStatus.Queued)
                    {
                        aggregate.Queued++;
                    }
                    if (message.Status == MessageStatus.Delivered || message.DeliveredAt.HasValue)
                    {
                        aggregate.Delivered++;
                    }
                    if (message.Status == MessageStatus.Opened || message.OpenedAt.HasValue)
                    {
                        aggregate.Opened++;
                    }
                    if (message.Status == MessageStatus.Failed || message.FailedAt.HasValue)
                    {
                        aggregate.Failed++;
                    }
                    if (message.Status == MessageStatus.Bounced)
                    {
                        aggregate.Bounced++;
                    }

                    lastKey = key;
                }
                else if (message.Direction == MessageDirection.In && lastKey is not null)
                {
                    if (aggregates.TryGetValue(lastKey.FullKey, out var aggregate))
                    {
                        aggregate.Replies++;
                    }
                }
            }
        }

        var byReplies = aggregates.Values
            .OrderByDescending(a => a.Replies)
            .ThenByDescending(a => a.Opened)
            .ThenByDescending(a => a.TotalSent)
            .Take(10)
            .Select(a => a.ToItem())
            .ToList();

        var byOpens = aggregates.Values
            .OrderByDescending(a => a.Opened)
            .ThenByDescending(a => a.TotalSent)
            .Take(10)
            .Select(a => a.ToItem())
            .ToList();

        return new TopMessagesResult(from, to, byReplies, byOpens);
    }

    private static void NormalizeRange(ref DateTime from, ref DateTime to)
    {
        if (to < from)
        {
            (from, to) = (to, from);
        }
    }

    private static TopMessageKey? ResolveKey(dynamic message)
    {
        if (message.TemplateName is string template && !string.IsNullOrWhiteSpace(template))
        {
            return new TopMessageKey(
                $"template:{template}".ToLowerInvariant(),
                template,
                message.Channel.ToString().ToLowerInvariant());
        }

        var subject = TryExtractSubject(message.PayloadJson);
        if (!string.IsNullOrWhiteSpace(subject))
        {
            return new TopMessageKey(
                $"subject:{subject}".ToLowerInvariant(),
                subject!,
                message.Channel.ToString().ToLowerInvariant());
        }

        return null;
    }

    private static string? TryExtractSubject(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                if (doc.RootElement.TryGetProperty("subject", out var subjectProp) && subjectProp.ValueKind == JsonValueKind.String)
                {
                    return subjectProp.GetString();
                }

                if (doc.RootElement.TryGetProperty("title", out var titleProp) && titleProp.ValueKind == JsonValueKind.String)
                {
                    return titleProp.GetString();
                }

                if (doc.RootElement.TryGetProperty("text", out var textProp) && textProp.ValueKind == JsonValueKind.String)
                {
                    var text = textProp.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text.Length <= 60 ? text : text[..60] + "…";
                    }
                }
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private sealed record TopMessageKey(string FullKey, string Label, string Channel);

    private sealed class TopMessageAggregate
    {
        public TopMessageAggregate(TopMessageKey key)
        {
            Key = key;
        }

        private TopMessageKey Key { get; }
        public int TotalSent { get; set; }
        public int Queued { get; set; }
        public int Sent { get; set; }
        public int Delivered { get; set; }
        public int Opened { get; set; }
        public int Failed { get; set; }
        public int Bounced { get; set; }
        public int Replies { get; set; }

        public TopMessageItem ToItem() => new(
            Key.FullKey,
            Key.Label,
            Key.Channel,
            TotalSent,
            Delivered,
            Opened,
            Failed,
            Bounced,
            Replies);
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

    private sealed class DeliveryTimelineRow
    {
        public DateTime Date { get; init; }
        public int Queued { get; init; }
        public int Sent { get; init; }
        public int Delivered { get; init; }
        public int Opened { get; init; }
        public int Failed { get; init; }
        public int Bounced { get; init; }
    }
}

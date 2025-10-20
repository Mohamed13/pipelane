using System.Globalization;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Pipelane.Application.Storage;
using Pipelane.Domain.Entities;
using Pipelane.Domain.Enums;

namespace Pipelane.Infrastructure.Background;

public sealed record GuardResult(bool CanSend, DateTime? RescheduleToUtc = null, string? FailureCode = null, string? FailureReason = null);

public interface IMessageDispatchGuard
{
    Task<GuardResult> EvaluateAsync(OutboxMessage job, Contact contact, Conversation convo, CancellationToken ct);
}

public sealed class MessageDispatchGuard : IMessageDispatchGuard
{
    private readonly IAppDbContext _db;
    private readonly MessagingLimitsOptions _options;
    private readonly TimeProvider _clock;

    public MessageDispatchGuard(IAppDbContext db, IOptions<MessagingLimitsOptions> options, TimeProvider? clock = null)
    {
        _db = db;
        _options = options.Value;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<GuardResult> EvaluateAsync(OutboxMessage job, Contact contact, Conversation convo, CancellationToken ct)
    {
        if (IsOptedOut(contact, job.Channel))
        {
            return new GuardResult(false, FailureCode: "opt_out", FailureReason: "Contact opted-out of this channel.");
        }

        if (job.Channel == Channel.Whatsapp && job.Type != MessageType.Template)
        {
            var lastInbound = await _db.Messages
                .Where(m => m.ConversationId == convo.Id && m.Channel == Channel.Whatsapp && m.Direction == MessageDirection.In)
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => (DateTime?)m.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (!lastInbound.HasValue || (_clock.GetUtcNow().UtcDateTime - lastInbound.Value) > TimeSpan.FromHours(24))
            {
                return new GuardResult(false, FailureCode: "whatsapp_session_expired", FailureReason: "WhatsApp session older than 24h.");
            }
        }

        var tenantId = contact.TenantId;
        var todayUtc = _clock.GetUtcNow().UtcDateTime.Date;
        var sentToday = await _db.Messages
            .Where(m => m.TenantId == tenantId
                        && m.Direction == MessageDirection.Out
                        && m.CreatedAt >= todayUtc
                        && m.Status != MessageStatus.Failed)
            .CountAsync(ct);

        if (sentToday >= _options.DailySendCap)
        {
            var nextSend = GetNextDayAt1030(contact);
            return new GuardResult(false, RescheduleToUtc: nextSend);
        }

        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        if (job.ScheduledAtUtc.HasValue && job.ScheduledAtUtc.Value > nowUtc)
        {
            nowUtc = job.ScheduledAtUtc.Value;
        }

        if (IsInQuietHours(contact, nowUtc, out var resumeAtUtc))
        {
            return new GuardResult(false, RescheduleToUtc: resumeAtUtc);
        }

        return new GuardResult(true);
    }

    private bool IsOptedOut(Contact contact, Channel channel)
    {
        if (string.IsNullOrWhiteSpace(contact.TagsJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(contact.TagsJson);
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                var tags = document.RootElement.EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String)
                    .Select(e => e.GetString())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s!.ToLowerInvariant())
                    .ToList();
                return channel switch
                {
                    Channel.Email => tags.Contains("optout_email") || tags.Contains("stop_email"),
                    Channel.Sms => tags.Contains("optout_sms") || tags.Contains("stop_sms"),
                    _ => false
                };
            }
        }
        catch (JsonException)
        {
        }
        return false;
    }

    private bool IsInQuietHours(Contact contact, DateTime utcTimestamp, out DateTime resumeUtc)
    {
        var tz = ResolveTimezone(contact);
        var localTime = TimeZoneInfo.ConvertTimeFromUtc(utcTimestamp, tz);
        var start = _options.QuietHoursStart;
        var end = _options.QuietHoursEnd;

        var isQuiet = start <= end
            ? localTime.TimeOfDay >= start && localTime.TimeOfDay < end
            : localTime.TimeOfDay >= start || localTime.TimeOfDay < end;

        if (isQuiet)
        {
            var nextLocal = new DateTime(localTime.Year, localTime.Month, localTime.Day, 10, 30, 0, localTime.Kind);
            if (localTime.TimeOfDay >= start)
            {
                nextLocal = nextLocal.AddDays(1);
            }
            resumeUtc = TimeZoneInfo.ConvertTimeToUtc(nextLocal, tz);
            return true;
        }

        resumeUtc = utcTimestamp;
        return false;
    }

    private DateTime GetNextDayAt1030(Contact contact)
    {
        var tz = ResolveTimezone(contact);
        var local = TimeZoneInfo.ConvertTimeFromUtc(_clock.GetUtcNow().UtcDateTime, tz).AddDays(1);
        var target = new DateTime(local.Year, local.Month, local.Day, 10, 30, 0, local.Kind);
        return TimeZoneInfo.ConvertTimeToUtc(target, tz);
    }

    private static TimeZoneInfo ResolveTimezone(Contact contact)
    {
        if (string.IsNullOrWhiteSpace(contact.TagsJson))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            using var document = JsonDocument.Parse(contact.TagsJson);
            if (document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("timezone", out var tzProp))
            {
                var tzId = tzProp.GetString();
                if (!string.IsNullOrWhiteSpace(tzId))
                {
                    return GetTimezoneOrUtc(tzId);
                }
            }

            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in document.RootElement.EnumerateArray())
                {
                    if (element.ValueKind == JsonValueKind.String)
                    {
                        var value = element.GetString();
                        if (!string.IsNullOrWhiteSpace(value) && value.StartsWith("tz:", StringComparison.OrdinalIgnoreCase))
                        {
                            var tzId = value[3..];
                            return GetTimezoneOrUtc(tzId);
                        }
                    }
                }
            }
        }
        catch (JsonException)
        {
        }

        return TimeZoneInfo.Utc;
    }

    private static TimeZoneInfo GetTimezoneOrUtc(string tzId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(tzId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}

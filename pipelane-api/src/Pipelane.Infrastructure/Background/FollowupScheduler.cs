using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Pipelane.Application.Abstractions;
using Pipelane.Application.Services;
using Pipelane.Application.Storage;
using Pipelane.Domain.Entities;
using Pipelane.Domain.Enums;

using Quartz;

namespace Pipelane.Infrastructure.Background;

public sealed class FollowupScheduler : IJob
{
    private static readonly string FollowupTitle = "Follow up";

    private readonly IServiceProvider _sp;
    private readonly ILogger<FollowupScheduler> _logger;

    public FollowupScheduler(IServiceProvider sp, ILogger<FollowupScheduler> logger)
    {
        _sp = sp;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task Execute(IJobExecutionContext context)
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxService>();
        var cancellationToken = context.CancellationToken;

        try
        {
            var now = DateTime.UtcNow;
            await CreateFollowupTasksAsync(db, now, cancellationToken);
            await EnqueueNudgesAsync(db, outbox, now, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Followup scheduler job failed");
            throw;
        }
    }

    private static async Task CreateFollowupTasksAsync(IAppDbContext db, DateTime now, CancellationToken ct)
    {
        var openedThreshold = now.AddHours(-24);

        var candidates = await (
                from m in db.Messages
                join convo in db.Conversations on m.ConversationId equals convo.Id
                where m.Direction == MessageDirection.Out
                      && m.Status == MessageStatus.Opened
                      && m.OpenedAt != null
                      && m.OpenedAt <= openedThreshold
                      && !db.Messages.Any(im => im.ConversationId == m.ConversationId && im.Direction == MessageDirection.In && im.CreatedAt >= m.OpenedAt)
                      && !db.FollowupTasks.Any(t => t.MessageId == m.Id)
                select new { MessageId = m.Id, m.TenantId, convo.ContactId }
            )
            .AsNoTracking()
            .Take(100)
            .ToListAsync(ct);

        if (candidates.Count == 0)
        {
            return;
        }

        foreach (var candidate in candidates)
        {
            db.FollowupTasks.Add(new FollowupTask
            {
                Id = Guid.NewGuid(),
                TenantId = candidate.TenantId,
                ContactId = candidate.ContactId,
                MessageId = candidate.MessageId,
                Title = FollowupTitle,
                DueAtUtc = now.AddHours(24),
                CreatedAtUtc = now
            });
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task EnqueueNudgesAsync(IAppDbContext db, IOutboxService outbox, DateTime now, CancellationToken ct)
    {
        var nudgeThreshold = now.AddHours(-48);

        var candidates = await (
                from m in db.Messages
                join convo in db.Conversations on m.ConversationId equals convo.Id
                where m.Direction == MessageDirection.Out
                      && (m.Status == MessageStatus.Sent || m.Status == MessageStatus.Delivered)
                      && m.CreatedAt <= nudgeThreshold
                      && !db.Messages.Any(im => im.ConversationId == m.ConversationId && im.Direction == MessageDirection.In && im.CreatedAt >= m.CreatedAt)
                      && !db.Messages.Any(n => n.ConversationId == m.ConversationId && n.Direction == MessageDirection.Out && n.TemplateName == "nudge-1" && n.CreatedAt >= m.CreatedAt)
                select new { Message = m, convo.ContactId }
            )
            .AsNoTracking()
            .Take(100)
            .ToListAsync(ct);

        if (candidates.Count == 0)
        {
            return;
        }

        var templateCache = new Dictionary<(Guid TenantId, Channel Channel), Template?>();

        foreach (var candidate in candidates)
        {
            var key = (candidate.Message.TenantId, candidate.Message.Channel);
            if (!templateCache.TryGetValue(key, out var template))
            {
                template = await db.Templates.FirstOrDefaultAsync(t => t.TenantId == candidate.Message.TenantId && t.Name == "nudge-1" && t.Channel == candidate.Message.Channel, ct);
                templateCache[key] = template;
            }

            if (template is null)
            {
                continue;
            }

            var alreadyQueued = await db.Outbox.AnyAsync(o => o.ContactId == candidate.ContactId && o.TemplateId == template.Id && o.Status != OutboxStatus.Done, ct);
            if (alreadyQueued)
            {
                continue;
            }

            var message = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                TenantId = candidate.Message.TenantId,
                ContactId = candidate.ContactId,
                ConversationId = candidate.Message.ConversationId,
                Channel = candidate.Message.Channel,
                Type = MessageType.Template,
                TemplateId = template.Id,
                PayloadJson = "{}",
                ScheduledAtUtc = null,
                CreatedAt = now
            };

            await outbox.EnqueueAsync(message, ct);
        }
    }
}

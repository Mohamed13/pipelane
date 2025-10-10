using System.Collections.Generic;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Pipelane.Application.Abstractions;
using Pipelane.Application.Services;
using Pipelane.Application.Storage;
using Pipelane.Domain.Entities;
using Pipelane.Domain.Enums;
using Pipelane.Infrastructure.Channels;

namespace Pipelane.Infrastructure.Background;

public class OutboxProcessor : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(IServiceProvider sp, ILogger<OutboxProcessor> logger)
    { _sp = sp; _logger = logger; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
                var registry = scope.ServiceProvider.GetRequiredService<IChannelRegistry>();

                var now = DateTime.UtcNow;
                var batch = await db.Outbox
                    .Where(o => (o.Status == OutboxStatus.Queued || (o.Status == OutboxStatus.Failed && o.Attempts < o.MaxAttempts))
                                 && (o.ScheduledAtUtc == null || o.ScheduledAtUtc <= now)
                                 && (o.LockedUntilUtc == null || o.LockedUntilUtc < now))
                    .OrderBy(o => o.CreatedAt)
                    .Take(10)
                    .ToListAsync(stoppingToken);

                foreach (var item in batch)
                {
                    item.Status = OutboxStatus.Sending;
                    item.LockedUntilUtc = now.AddSeconds(30);
                }
                if (batch.Count > 0)
                {
                    await db.SaveChangesAsync(stoppingToken);
                }

                foreach (var job in batch)
                {
                    await ProcessJobAsync(job, db, registry, stoppingToken);
                }

                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox loop error");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task ProcessJobAsync(OutboxMessage job, IAppDbContext db, IChannelRegistry registry, CancellationToken ct)
    {
        var maxAttempts = job.MaxAttempts <= 0 ? 5 : job.MaxAttempts;
        var contact = await db.Contacts.FindAsync(new object?[] { job.ContactId }, ct);
        if (contact is null)
        {
            job.Status = OutboxStatus.Failed;
            job.LockedUntilUtc = null;
            job.LastError = "Contact missing";
            job.Attempts = maxAttempts;
            await db.SaveChangesAsync(ct);
            return;
        }

        var channel = registry.Resolve(job.Channel);
        if (channel is null)
        {
            job.Status = OutboxStatus.Failed;
            job.LockedUntilUtc = null;
            job.LastError = "Channel not found";
            job.Attempts = maxAttempts;
            await db.SaveChangesAsync(ct);
            return;
        }

        Template? template = null;
        string? templateName = null;
        if (job.Type == MessageType.Template)
        {
            if (!job.TemplateId.HasValue)
            {
                job.Status = OutboxStatus.Failed;
                job.LockedUntilUtc = null;
                job.LastError = "Template missing";
                job.Attempts = maxAttempts;
                await db.SaveChangesAsync(ct);
                return;
            }

            template = await db.Templates.FindAsync(new object?[] { job.TemplateId.Value }, ct);
            if (template is null)
            {
                job.Status = OutboxStatus.Failed;
                job.LockedUntilUtc = null;
                job.LastError = "Template missing";
                job.Attempts = maxAttempts;
                await db.SaveChangesAsync(ct);
                return;
            }
            templateName = template.Name;
        }

        var convo = job.ConversationId.HasValue
            ? await db.Conversations.FindAsync(new object?[] { job.ConversationId.Value }, ct)
            : null;

        if (convo is null)
        {
            convo = new Conversation
            {
                Id = Guid.NewGuid(),
                TenantId = contact.TenantId,
                ContactId = contact.Id,
                PrimaryChannel = job.Channel,
                CreatedAt = DateTime.UtcNow
            };
            db.Conversations.Add(convo);
            await db.SaveChangesAsync(ct);
            job.ConversationId = convo.Id;
        }

        var payload = JsonSerializer.Deserialize<Dictionary<string, string>>(job.PayloadJson) ?? new();
        var provider = channel switch
        {
            EmailChannel => "resend",
            _ => channel.GetType().Name
        };

        SendResult? lastResult = null;
        Exception? lastException = null;
        string? providerMessageId = null;
        string? errorReason = null;
        string? errorCode = null;
        var now = DateTime.UtcNow;

        while (job.Attempts < maxAttempts)
        {
            var attemptNumber = job.Attempts + 1;
            try
            {
                SendResult result;
                if (job.Type == MessageType.Template)
                {
                    result = await channel.SendTemplateAsync(contact, template!, payload, new SendMeta(job.ConversationId, null), ct);
                }
                else if (job.Type == MessageType.Text)
                {
                    var text = payload.TryGetValue("text", out var messageText) ? messageText : string.Empty;
                    result = await channel.SendTextAsync(contact, text, new SendMeta(job.ConversationId, null), ct);
                }
                else
                {
                    throw new NotSupportedException("Only text/template supported");
                }

                lastResult = result;
                providerMessageId ??= result.ProviderMessageId;
                job.Attempts = attemptNumber;
                job.LastError = result.Error;

                if (result.Success)
                {
                    await FinalizeMessageAsync(db, contact, convo, job, provider, providerMessageId, templateName, MessageStatus.Sent, null, null, ct);
                    return;
                }

                errorReason = result.Error;
                errorCode ??= "provider_error";
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                lastException = ex;
                job.LastError = ex.Message;
                job.Attempts = attemptNumber;
                errorReason = ex.Message;
                errorCode = ex.GetType().Name;
            }

            if (job.Attempts >= maxAttempts)
            {
                providerMessageId ??= lastResult?.ProviderMessageId;
                await FinalizeMessageAsync(db, contact, convo, job, provider, providerMessageId, templateName, MessageStatus.Failed, errorCode ?? "send_failed", errorReason, ct, failed: true);
                return;
            }

            var backoff = TimeSpan.FromSeconds(Math.Pow(2, job.Attempts));
            job.Status = OutboxStatus.Queued;
            job.LockedUntilUtc = DateTime.UtcNow.Add(backoff);
            await db.SaveChangesAsync(ct);
            return;
        }
    }

    private async Task FinalizeMessageAsync(IAppDbContext db, Contact contact, Conversation convo, OutboxMessage job, string provider, string? providerMessageId, string? templateName, MessageStatus status, string? errorCode, string? errorReason, CancellationToken ct, bool failed = false)
    {
        var timestamp = DateTime.UtcNow;
        var message = new Message
        {
            Id = Guid.NewGuid(),
            TenantId = contact.TenantId,
            ConversationId = convo.Id,
            Channel = job.Channel,
            Direction = MessageDirection.Out,
            Type = job.Type,
            TemplateName = templateName,
            PayloadJson = job.PayloadJson,
            Status = status,
            Provider = provider,
            ProviderMessageId = providerMessageId,
            ErrorCode = errorCode,
            ErrorReason = errorReason,
            DeliveredAt = status == MessageStatus.Delivered ? timestamp : null,
            OpenedAt = status == MessageStatus.Opened ? timestamp : null,
            FailedAt = failed ? timestamp : null,
            CreatedAt = timestamp
        };

        db.Messages.Add(message);
        db.MessageEvents.Add(new MessageEvent
        {
            Id = Guid.NewGuid(),
            TenantId = contact.TenantId,
            MessageId = message.Id,
            Type = failed ? MessageEventType.Failed : MessageEventType.Sent,
            Provider = provider,
            CreatedAt = timestamp
        });

        job.Status = failed ? OutboxStatus.Failed : OutboxStatus.Done;
        job.LockedUntilUtc = null;
        job.LastError = errorReason;
        await db.SaveChangesAsync(ct);
    }
}

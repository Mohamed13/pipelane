using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Pipelane.Application.Abstractions;
using Pipelane.Application.Services;
using Pipelane.Application.Storage;
using Pipelane.Domain.Entities;
using Pipelane.Domain.Enums;
using Pipelane.Infrastructure.Automations;
using Pipelane.Infrastructure.Channels;

namespace Pipelane.Infrastructure.Background;

/// <summary>
/// Centralises message dispatch so it can be triggered by both the background service loop and Quartz jobs.
/// </summary>
public sealed class OutboxDispatchExecutor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxDispatchExecutor> _logger;

    public OutboxDispatchExecutor(IServiceScopeFactory scopeFactory, ILogger<OutboxDispatchExecutor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<int> DispatchAsync(int batchSize, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var registry = scope.ServiceProvider.GetRequiredService<IChannelRegistry>();
        var guard = scope.ServiceProvider.GetRequiredService<IMessageDispatchGuard>();
        var publisher = scope.ServiceProvider.GetRequiredService<IAutomationEventPublisher>();

        var now = DateTime.UtcNow;
        var batch = await db.Outbox
            .Where(o => (o.Status == OutboxStatus.Queued || (o.Status == OutboxStatus.Failed && o.Attempts < o.MaxAttempts))
                         && (o.ScheduledAtUtc == null || o.ScheduledAtUtc <= now)
                         && (o.LockedUntilUtc == null || o.LockedUntilUtc < now))
            .OrderBy(o => o.CreatedAt)
            .Take(batchSize)
            .ToListAsync(ct);

        foreach (var item in batch)
        {
            item.Status = OutboxStatus.Sending;
            item.LockedUntilUtc = now.AddSeconds(30);
        }

        if (batch.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        var processed = 0;
        foreach (var job in batch)
        {
            var handled = await ProcessJobAsync(job, db, registry, guard, publisher, ct);
            if (handled)
            {
                processed++;
            }
        }

        return processed;
    }

    private async Task<bool> ProcessJobAsync(OutboxMessage job, IAppDbContext db, IChannelRegistry registry, IMessageDispatchGuard guard, IAutomationEventPublisher publisher, CancellationToken ct)
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
            return true;
        }

        var channel = registry.Resolve(job.Channel);
        if (channel is null)
        {
            job.Status = OutboxStatus.Failed;
            job.LockedUntilUtc = null;
            job.LastError = "Channel not found";
            job.Attempts = maxAttempts;
            await db.SaveChangesAsync(ct);
            return true;
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
                return true;
            }

            template = await db.Templates.FindAsync(new object?[] { job.TemplateId.Value }, ct);
            if (template is null)
            {
                job.Status = OutboxStatus.Failed;
                job.LockedUntilUtc = null;
                job.LastError = "Template missing";
                job.Attempts = maxAttempts;
                await db.SaveChangesAsync(ct);
                return true;
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

        var guardResult = await guard.EvaluateAsync(job, contact, convo, ct);
        if (!guardResult.CanSend)
        {
            if (guardResult.RescheduleToUtc.HasValue)
            {
                job.Status = OutboxStatus.Queued;
                job.ScheduledAtUtc = guardResult.RescheduleToUtc;
                job.LockedUntilUtc = null;
                await db.SaveChangesAsync(ct);
                _logger.LogInformation("Rescheduled message {MessageId} for tenant {TenantId} to {NextSend} due to guard", job.Id, contact.TenantId, guardResult.RescheduleToUtc);
                return false;
            }

            job.Status = OutboxStatus.Failed;
            job.Attempts = maxAttempts;
            job.LockedUntilUtc = null;
            job.LastError = guardResult.FailureReason;
            await db.SaveChangesAsync(ct);
            _logger.LogWarning("Blocked message {MessageId} for tenant {TenantId}: {Reason}", job.Id, contact.TenantId, guardResult.FailureReason);
            return true;
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
                    await FinalizeMessageAsync(db, contact, convo, job, provider, providerMessageId, templateName, MessageStatus.Sent, null, null, publisher, ct);
                    return true;
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
                await FinalizeMessageAsync(db, contact, convo, job, provider, providerMessageId, templateName, MessageStatus.Failed, errorCode ?? "send_failed", errorReason, publisher, ct, failed: true);
                if (lastException is not null)
                {
                    _logger.LogWarning(lastException, "Failed to dispatch message {MessageId} for tenant {TenantId}", job.Id, contact.TenantId);
                }
                return true;
            }

            var backoff = TimeSpan.FromSeconds(Math.Pow(2, job.Attempts));
            job.Status = OutboxStatus.Queued;
            job.LockedUntilUtc = DateTime.UtcNow.Add(backoff);
            await db.SaveChangesAsync(ct);
            return false;
        }

        return true;
    }

    private async Task FinalizeMessageAsync(
        IAppDbContext db,
        Contact contact,
        Conversation convo,
        OutboxMessage job,
        string provider,
        string? providerMessageId,
        string? templateName,
        MessageStatus status,
        string? errorCode,
        string? errorReason,
        IAutomationEventPublisher publisher,
        CancellationToken ct,
        bool failed = false)
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
        _logger.LogInformation("Message dispatched {TenantId} {ContactId} {Channel} status={Status}", contact.TenantId, contact.Id, job.Channel, status);

        var eventPayload = new
        {
            messageId = message.Id,
            contactId = contact.Id,
            channel = job.Channel.ToString().ToLowerInvariant(),
            status,
            provider,
            scheduledAt = job.ScheduledAtUtc,
            sentAt = timestamp,
            errorCode,
            errorReason
        };

        var statusEvent = failed ? "message.failed" : "message.sent";
        await publisher.PublishAsync(statusEvent, eventPayload, contact.TenantId, ct);
        await publisher.PublishAsync("message.status.changed", eventPayload, contact.TenantId, ct);
    }
}

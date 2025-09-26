using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Pipelane.Application.Abstractions;
using Pipelane.Application.Services;
using Pipelane.Application.Storage;
using Pipelane.Domain.Entities;
using Pipelane.Domain.Enums;

namespace Pipelane.Infrastructure.Background;

public class OutboxProcessor : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<OutboxProcessor> _logger;
    private readonly AsyncRetryPolicy _retry = Policy
        .Handle<Exception>()
        .WaitAndRetryAsync(3, i => TimeSpan.FromSeconds(Math.Pow(2, i)));

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
                    await _retry.ExecuteAsync(async ct =>
                    {
                        try
                        {
                            var contact = await db.Contacts.FindAsync(new object?[] { job.ContactId }, ct) ?? throw new InvalidOperationException("Contact missing");
                            var channel = registry.Resolve(job.Channel) ?? throw new InvalidOperationException("Channel not found");
                            SendResult result;
                            if (job.Type == MessageType.Template)
                            {
                                var template = await db.Templates.FindAsync(new object?[] { job.TemplateId!.Value }, ct) ?? throw new InvalidOperationException("Template missing");
                                var vars = JsonSerializer.Deserialize<Dictionary<string,string>>(job.PayloadJson) ?? new();
                                result = await channel.SendTemplateAsync(contact, template, vars, new SendMeta(job.ConversationId, null), ct);
                            }
                            else if (job.Type == MessageType.Text)
                            {
                                var vars = JsonSerializer.Deserialize<Dictionary<string,string>>(job.PayloadJson) ?? new();
                                var text = vars.TryGetValue("text", out var t) ? t : string.Empty;
                                result = await channel.SendTextAsync(contact, text, new SendMeta(job.ConversationId, null), ct);
                            }
                            else
                            {
                                throw new NotSupportedException("Only text/template supported");
                            }

                            var convo = job.ConversationId.HasValue ? await db.Conversations.FindAsync(new object?[] { job.ConversationId.Value }, ct) : null;
                            if (convo == null)
                            {
                                convo = new Conversation { Id = Guid.NewGuid(), TenantId = contact.TenantId, ContactId = contact.Id, PrimaryChannel = job.Channel, CreatedAt = DateTime.UtcNow };
                                db.Conversations.Add(convo);
                                await db.SaveChangesAsync(ct);
                                job.ConversationId = convo.Id;
                            }

                            db.Messages.Add(new Message
                            {
                                Id = Guid.NewGuid(),
                                TenantId = contact.TenantId,
                                ConversationId = convo.Id,
                                Channel = job.Channel,
                                Direction = MessageDirection.Out,
                                Type = job.Type,
                                TemplateName = job.Type == MessageType.Template ? (await db.Templates.FindAsync(new object?[] { job.TemplateId!.Value }, ct))?.Name : null,
                                PayloadJson = job.PayloadJson,
                                Status = result.Success ? MessageStatus.Sent : MessageStatus.Failed,
                                ProviderMessageId = result.ProviderMessageId,
                                CreatedAt = DateTime.UtcNow
                            });

                            job.Status = result.Success ? OutboxStatus.Done : OutboxStatus.Failed;
                            job.Attempts += 1;
                            job.LastError = result.Error;
                            job.LockedUntilUtc = null;
                            await db.SaveChangesAsync(ct);
                        }
                        catch (Exception ex)
                        {
                            job.Status = OutboxStatus.Failed;
                            job.Attempts += 1;
                            job.LastError = ex.Message;
                            job.LockedUntilUtc = null;
                            await db.SaveChangesAsync(ct);
                            throw;
                        }
                    }, stoppingToken);
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
}

using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Pipelane.Application.Abstractions;
using Pipelane.Application.DTOs;
using Pipelane.Application.Storage;
using Pipelane.Domain.Entities;
using Pipelane.Domain.Enums;

namespace Pipelane.Application.Services;

public interface IChannelRegistry
{
    IMessageChannel? Resolve(Channel channel);
}

public interface IOutboxService
{
    Task EnqueueAsync(OutboxMessage msg, CancellationToken ct);
}

public sealed class OutboxService : IOutboxService
{
    private readonly IAppDbContext _db;
    public OutboxService(IAppDbContext db) => _db = db;
    public async Task EnqueueAsync(OutboxMessage msg, CancellationToken ct)
    {
        await _db.Outbox.AddAsync(msg, ct);
        await _db.SaveChangesAsync(ct);
    }
}

public sealed class ChannelRegistry : IChannelRegistry
{
    private readonly IEnumerable<IMessageChannel> _channels;
    public ChannelRegistry(IEnumerable<IMessageChannel> channels) => _channels = channels;
    public IMessageChannel? Resolve(Channel channel) => _channels.FirstOrDefault(c => c.Channel == channel);
}

public interface IMessagingService
{
    Task<SendResult> SendAsync(SendMessageRequest req, CancellationToken ct);
}

public sealed class MessagingService : IMessagingService
{
    private readonly IAppDbContext _db;
    private readonly IChannelRegistry _registry;
    private readonly IChannelRulesService _rules;
    private readonly IOutboxService _outbox;

    public MessagingService(IAppDbContext db, IChannelRegistry registry, IChannelRulesService rules, IOutboxService outbox)
    {
        _db = db; _registry = registry; _rules = rules; _outbox = outbox;
    }

    public async Task<SendResult> SendAsync(SendMessageRequest req, CancellationToken ct)
    {
        var contact = await ResolveContactAsync(req, ct) ?? throw new InvalidOperationException("Contact not found");

        if (req.Type.Equals("text", StringComparison.OrdinalIgnoreCase))
        {
            // WhatsApp session rule
            if (req.Channel == Channel.Whatsapp && !await _rules.CanSendWhatsAppSessionAsync(contact.Id, ct))
            {
                // require template outside 24h: enqueue as failed with message
                return new SendResult(false, null, "Outside 24h window: template required");
            }
            var ch = _registry.Resolve(req.Channel) ?? throw new InvalidOperationException("Channel not available");
            return await ch.SendTextAsync(contact, req.Text ?? string.Empty, new SendMeta(null, null), ct);
        }

        if (req.Type.Equals("template", StringComparison.OrdinalIgnoreCase))
        {
            var template = await _db.Templates.FirstAsync(t => t.TenantId == contact.TenantId && t.Name == (req.TemplateName ?? "") && t.Channel == req.Channel && t.Lang == (req.Lang ?? "en"), ct);
            var vars = req.Variables ?? new Dictionary<string, string>();

            // enqueue to outbox for reliability
            var outbox = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                TenantId = contact.TenantId,
                ContactId = contact.Id,
                Channel = req.Channel,
                Type = MessageType.Template,
                TemplateId = template.Id,
                PayloadJson = JsonSerializer.Serialize(vars),
                MetaJson = req.Meta is null ? null : JsonSerializer.Serialize(req.Meta),
                CreatedAt = DateTime.UtcNow,
            };
            await _outbox.EnqueueAsync(outbox, ct);
            return new SendResult(true, null, null);
        }

        throw new NotSupportedException("Unsupported message type");
    }

    private async Task<Contact?> ResolveContactAsync(SendMessageRequest req, CancellationToken ct)
    {
        if (req.ContactId.HasValue)
            return await _db.Contacts.FindAsync(new object?[] { req.ContactId.Value }, ct);
        if (!string.IsNullOrWhiteSpace(req.Phone))
            return await _db.Contacts.FirstOrDefaultAsync(c => c.Phone == req.Phone, ct);
        return null;
    }
}

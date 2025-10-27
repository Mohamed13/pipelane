using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Pipelane.Application.Services;
using Pipelane.Application.Storage;
using Pipelane.Domain.Entities;
using Pipelane.Domain.Enums;

namespace Pipelane.Infrastructure.Demo;

public sealed class DemoOptions
{
    public bool Enabled { get; set; }
}

public sealed class DemoExperienceService : IDemoExperienceService
{
    private readonly IAppDbContext _db;
    private readonly TimeProvider _clock;
    private readonly IOptions<DemoOptions> _options;
    private readonly ILogger<DemoExperienceService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly IReadOnlyList<DemoRecipient> DefaultRecipients =
    [
        new DemoRecipient(
            Key: "email",
            FirstName: "Mila",
            LastName: "Leroy",
            Email: "self-test+email@pipelane.dev",
            Phone: null,
            Channel: Channel.Email,
            Status: MessageStatus.Opened,
            OutboundText: "Bonjour {{firstName}}, voici un aperçu des relances intelligentes prêtes pour votre équipe.",
            InboundText: "Parfait, envoyez-moi les stats détaillées.",
            CreatedOffsetMinutes: -6,
            OpenedOffsetMinutes: -4),
        new DemoRecipient(
            Key: "whatsapp",
            FirstName: "Jules",
            LastName: "Martel",
            Email: null,
            Phone: "+15550001001",
            Channel: Channel.Whatsapp,
            Status: MessageStatus.Delivered,
            OutboundText: "Jules, on a une fenêtre de 10h-12h demain qui convertit super bien. On envoie ?",
            InboundText: null,
            CreatedOffsetMinutes: -4,
            OpenedOffsetMinutes: null),
        new DemoRecipient(
            Key: "sms",
            FirstName: "Reese",
            LastName: "Lambert",
            Email: null,
            Phone: "+15550001002",
            Channel: Channel.Sms,
            Status: MessageStatus.Sent,
            OutboundText: "Reese, il reste 3 contacts chauds sans réponse. On déclenche une relance courte ?",
            InboundText: null,
            CreatedOffsetMinutes: -2,
            OpenedOffsetMinutes: null)
    ];

    public DemoExperienceService(
        IAppDbContext db,
        TimeProvider? clock,
        IOptions<DemoOptions> options,
        ILogger<DemoExperienceService> logger)
    {
        _db = db;
        _clock = clock ?? TimeProvider.System;
        _options = options;
        _logger = logger;
    }

    public async Task<DemoRunResult> RunAsync(Guid tenantId, CancellationToken ct)
    {
        if (!_options.Value.Enabled)
        {
            throw new InvalidOperationException("Demo mode is disabled.");
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var outcomes = new List<DemoRunMessage>();

        foreach (var recipient in DefaultRecipients)
        {
            var contact = await EnsureContactAsync(tenantId, recipient, now, ct).ConfigureAwait(false);
            var conversation = await EnsureConversationAsync(tenantId, contact.Id, recipient.Channel, now, ct).ConfigureAwait(false);

            var message = CreateOutboundMessage(tenantId, conversation.Id, recipient, now);
            _db.Messages.Add(message);

            foreach (var evt in BuildEvents(tenantId, message, recipient, now))
            {
                _db.MessageEvents.Add(evt);
            }

            if (!string.IsNullOrWhiteSpace(recipient.InboundText))
            {
                var inboundCreatedAt = now.AddMinutes(Math.Max(recipient.CreatedOffsetMinutes + 2, -1));
                var inbound = new Message
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ConversationId = conversation.Id,
                    Channel = recipient.Channel,
                    Direction = MessageDirection.In,
                    Type = MessageType.Text,
                    Status = MessageStatus.Delivered,
                    PayloadJson = JsonSerializer.Serialize(new { text = recipient.InboundText }, JsonOptions),
                    Provider = "demo",
                    CreatedAt = inboundCreatedAt,
                    DeliveredAt = inboundCreatedAt
                };
                _db.Messages.Add(inbound);

                _db.MessageEvents.Add(new MessageEvent
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    MessageId = inbound.Id,
                    Type = MessageEventType.Delivered,
                    Provider = "demo",
                    CreatedAt = inboundCreatedAt
                });
            }

            outcomes.Add(new DemoRunMessage(
                contact.Id,
                conversation.Id,
                message.Id,
                recipient.Channel,
                $"{contact.FirstName ?? string.Empty} {contact.LastName ?? string.Empty}".Trim(),
                message.Status,
                message.CreatedAt));
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        _logger.LogInformation("Demo data generated for tenant {TenantId} ({Count} messages)", tenantId, outcomes.Count);

        return new DemoRunResult(now, outcomes);
    }

    private async Task<Contact> EnsureContactAsync(Guid tenantId, DemoRecipient recipient, DateTime now, CancellationToken ct)
    {
        Contact? existing = null;
        if (!string.IsNullOrWhiteSpace(recipient.Email))
        {
            existing = await _db.Contacts
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Email == recipient.Email, ct)
                .ConfigureAwait(false);
        }
        else if (!string.IsNullOrWhiteSpace(recipient.Phone))
        {
            existing = await _db.Contacts
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Phone == recipient.Phone, ct)
                .ConfigureAwait(false);
        }

        if (existing is not null)
        {
            existing.FirstName ??= recipient.FirstName;
            existing.LastName ??= recipient.LastName;
            existing.UpdatedAt = now;
            return existing;
        }

        var contact = new Contact
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = recipient.Email,
            Phone = recipient.Phone,
            FirstName = recipient.FirstName,
            LastName = recipient.LastName,
            Lang = "fr",
            TagsJson = "[\"demo\",\"self-test\"]",
            CreatedAt = now.AddMinutes(recipient.CreatedOffsetMinutes),
            UpdatedAt = now
        };

        _db.Contacts.Add(contact);
        return contact;
    }

    private async Task<Conversation> EnsureConversationAsync(Guid tenantId, Guid contactId, Channel channel, DateTime now, CancellationToken ct)
    {
        var existing = await _db.Conversations
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.ContactId == contactId && c.PrimaryChannel == channel, ct)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            return existing;
        }

        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ContactId = contactId,
            PrimaryChannel = channel,
            CreatedAt = now.AddMinutes(-10)
        };

        _db.Conversations.Add(conversation);
        return conversation;
    }

    private static Message CreateOutboundMessage(Guid tenantId, Guid conversationId, DemoRecipient recipient, DateTime now)
    {
        var createdAt = now.AddMinutes(recipient.CreatedOffsetMinutes);
        var deliveredAt = recipient.Status >= MessageStatus.Delivered
            ? now.AddMinutes(recipient.OpenedOffsetMinutes ?? recipient.CreatedOffsetMinutes + 1)
            : (DateTime?)null;

        var openedAt = recipient.Status == MessageStatus.Opened
            ? now.AddMinutes(recipient.OpenedOffsetMinutes ?? recipient.CreatedOffsetMinutes + 2)
            : (DateTime?)null;

        var payload = JsonSerializer.Serialize(
            new
            {
                text = recipient.OutboundText,
                angle = "value",
                template = "demo"
            },
            JsonOptions);

        return new Message
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ConversationId = conversationId,
            Channel = recipient.Channel,
            Direction = MessageDirection.Out,
            Type = MessageType.Text,
            TemplateName = "demo-sequence",
            Lang = "fr",
            PayloadJson = payload,
            Status = recipient.Status,
            Provider = "demo",
            CreatedAt = createdAt,
            DeliveredAt = deliveredAt,
            OpenedAt = openedAt
        };
    }

    private static IEnumerable<MessageEvent> BuildEvents(Guid tenantId, Message message, DemoRecipient recipient, DateTime now)
    {
        var baseCreated = message.CreatedAt;
        yield return new MessageEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            MessageId = message.Id,
            Type = MessageEventType.Sent,
            Provider = "demo",
            CreatedAt = baseCreated
        };

        if (recipient.Status >= MessageStatus.Delivered)
        {
            var deliveredAt = message.DeliveredAt ?? baseCreated.AddMinutes(1);
            yield return new MessageEvent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                MessageId = message.Id,
                Type = MessageEventType.Delivered,
                Provider = "demo",
                CreatedAt = deliveredAt
            };
        }

        if (recipient.Status == MessageStatus.Opened)
        {
            var openedAt = message.OpenedAt ?? (message.DeliveredAt ?? baseCreated).AddMinutes(1);
            yield return new MessageEvent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                MessageId = message.Id,
                Type = MessageEventType.Opened,
                Provider = "demo",
                CreatedAt = openedAt
            };
        }
    }

    private sealed record DemoRecipient(
        string Key,
        string FirstName,
        string LastName,
        string? Email,
        string? Phone,
        Channel Channel,
        MessageStatus Status,
        string OutboundText,
        string? InboundText,
        int CreatedOffsetMinutes,
        int? OpenedOffsetMinutes);
}

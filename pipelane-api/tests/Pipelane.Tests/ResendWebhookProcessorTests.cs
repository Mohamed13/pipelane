using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using Pipelane.Domain.Entities;
using Pipelane.Domain.Enums;
using Pipelane.Infrastructure.Webhooks;

using Xunit;

namespace Pipelane.Tests;

public class ResendWebhookProcessorTests
{
    [Fact]
    public async Task ProcessAsync_DeliveredEvent_UpdatesMessageStatus()
    {
        var options = new DbContextOptionsBuilder<FakeDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new FakeDbContext(options);

        var tenantId = Guid.NewGuid();
        var message = new Message
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ConversationId = Guid.NewGuid(),
            Channel = Channel.Email,
            Direction = MessageDirection.Out,
            Type = MessageType.Template,
            Status = MessageStatus.Sent,
            ProviderMessageId = "email_123",
            CreatedAt = DateTime.UtcNow
        };
        db.Messages.Add(message);
        await db.SaveChangesAsync();

        var payload = JsonSerializer.Serialize(new
        {
            type = "email.delivered",
            id = "evt_123",
            created_at = "2025-10-10T10:00:00Z",
            data = new
            {
                id = "email_123",
                email_id = "email_123",
                delivered_at = "2025-10-10T10:00:00Z"
            }
        });

        var processor = new ResendWebhookProcessor(db, NullLogger<ResendWebhookProcessor>.Instance);
        var result = await processor.ProcessAsync(payload, CancellationToken.None);

        result.Should().BeTrue();
        message.Status.Should().Be(MessageStatus.Delivered);
        message.DeliveredAt.Should().NotBeNull();
        db.MessageEvents.Count().Should().Be(1);
        var evt = db.MessageEvents.Single();
        evt.Type.Should().Be(MessageEventType.Delivered);
        evt.ProviderEventId.Should().Be("evt_123");

        // idempotent
        await processor.ProcessAsync(payload, CancellationToken.None);
        db.MessageEvents.Count().Should().Be(1);
    }

    [Fact]
    public async Task ProcessAsync_BouncedEvent_SetsErrorDetails()
    {
        var options = new DbContextOptionsBuilder<FakeDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new FakeDbContext(options);

        var tenantId = Guid.NewGuid();
        var message = new Message
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ConversationId = Guid.NewGuid(),
            Channel = Channel.Email,
            Direction = MessageDirection.Out,
            Type = MessageType.Template,
            Status = MessageStatus.Sent,
            ProviderMessageId = "email_456",
            CreatedAt = DateTime.UtcNow
        };
        db.Messages.Add(message);
        await db.SaveChangesAsync();

        var payload = JsonSerializer.Serialize(new
        {
            type = "email.bounced",
            data = new
            {
                id = "email_456",
                email_id = "email_456",
                bounce = new
                {
                    type = "hard",
                    description = "Mailbox unavailable"
                }
            }
        });

        var processor = new ResendWebhookProcessor(db, NullLogger<ResendWebhookProcessor>.Instance);
        await processor.ProcessAsync(payload, CancellationToken.None);

        message.Status.Should().Be(MessageStatus.Bounced);
        message.FailedAt.Should().NotBeNull();
        message.ErrorCode.Should().Be("hard");
        message.ErrorReason.Should().Be("Mailbox unavailable");
    }
}

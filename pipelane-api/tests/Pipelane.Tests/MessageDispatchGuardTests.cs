using System;
using System.Threading;
using System.Threading.Tasks;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Pipelane.Domain.Entities;
using Pipelane.Domain.Enums;
using Pipelane.Infrastructure.Background;

using Xunit;

namespace Pipelane.Tests;

public class MessageDispatchGuardTests
{
    [Fact]
    public async Task DailyCapReached_ShouldReschedule()
    {
        var options = new DbContextOptionsBuilder<FakeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new FakeDbContext(options);

        var tenantId = Guid.NewGuid();
        var contact = new Contact { Id = Guid.NewGuid(), TenantId = tenantId, Email = "user@example.com", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var convo = new Conversation { Id = Guid.NewGuid(), TenantId = tenantId, ContactId = contact.Id, PrimaryChannel = Channel.Email, CreatedAt = DateTime.UtcNow };
        db.Contacts.Add(contact);
        db.Conversations.Add(convo);

        var today = new DateTime(2025, 1, 10, 8, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 100; i++)
        {
            db.Messages.Add(new Message
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ConversationId = convo.Id,
                Channel = Channel.Email,
                Direction = MessageDirection.Out,
                Type = MessageType.Text,
                Status = MessageStatus.Sent,
                CreatedAt = today.AddMinutes(i)
            });
        }
        await db.SaveChangesAsync();

        var guard = new MessageDispatchGuard(db, Options.Create(new MessagingLimitsOptions { DailySendCap = 100 }), new FixedTimeProvider(today.AddHours(4)));

        var job = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ContactId = contact.Id,
            Channel = Channel.Email,
            Type = MessageType.Text,
            PayloadJson = "{}",
            CreatedAt = today
        };

        var result = await guard.EvaluateAsync(job, contact, convo, CancellationToken.None);

        result.CanSend.Should().BeFalse();
        result.RescheduleToUtc.Should().NotBeNull();
        result.FailureCode.Should().BeNull();
        result.RescheduleToUtc!.Value.TimeOfDay.Should().Be(new TimeSpan(10, 30, 0));
    }

    [Fact]
    public async Task QuietHours_ShouldRescheduleToMorning()
    {
        var options = new DbContextOptionsBuilder<FakeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new FakeDbContext(options);

        var tenantId = Guid.NewGuid();
        var contact = new Contact
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = "night@example.com",
            TagsJson = "{\"timezone\":\"Europe/Paris\"}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var convo = new Conversation { Id = Guid.NewGuid(), TenantId = tenantId, ContactId = contact.Id, PrimaryChannel = Channel.Email, CreatedAt = DateTime.UtcNow };
        db.Contacts.Add(contact);
        db.Conversations.Add(convo);
        await db.SaveChangesAsync();

        var utcNow = new DateTime(2025, 1, 10, 21, 30, 0, DateTimeKind.Utc); // 22:30 Paris
        var guard = new MessageDispatchGuard(db, Options.Create(new MessagingLimitsOptions { DailySendCap = 100 }), new FixedTimeProvider(utcNow));

        var job = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ContactId = contact.Id,
            Channel = Channel.Email,
            Type = MessageType.Text,
            PayloadJson = "{}",
            CreatedAt = utcNow
        };

        var result = await guard.EvaluateAsync(job, contact, convo, CancellationToken.None);

        result.CanSend.Should().BeFalse();
        result.RescheduleToUtc.Should().NotBeNull();
        var rescheduledLocal = TimeZoneInfo.ConvertTimeFromUtc(result.RescheduleToUtc!.Value, TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris"));
        rescheduledLocal.TimeOfDay.Should().Be(new TimeSpan(10, 30, 0));
    }

    [Fact]
    public async Task WhatsAppSessionExpired_ShouldBlock()
    {
        var options = new DbContextOptionsBuilder<FakeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new FakeDbContext(options);

        var tenantId = Guid.NewGuid();
        var contact = new Contact { Id = Guid.NewGuid(), TenantId = tenantId, Phone = "+1234567890", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var convo = new Conversation { Id = Guid.NewGuid(), TenantId = tenantId, ContactId = contact.Id, PrimaryChannel = Channel.Whatsapp, CreatedAt = DateTime.UtcNow.AddDays(-3) };
        db.Contacts.Add(contact);
        db.Conversations.Add(convo);
        db.Messages.Add(new Message
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ConversationId = convo.Id,
            Channel = Channel.Whatsapp,
            Direction = MessageDirection.In,
            Type = MessageType.Text,
            Status = MessageStatus.Sent,
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        });
        await db.SaveChangesAsync();

        var guard = new MessageDispatchGuard(db, Options.Create(new MessagingLimitsOptions()), new FixedTimeProvider(DateTime.UtcNow));

        var job = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ContactId = contact.Id,
            Channel = Channel.Whatsapp,
            Type = MessageType.Text,
            PayloadJson = "{}",
            CreatedAt = DateTime.UtcNow
        };

        var result = await guard.EvaluateAsync(job, contact, convo, CancellationToken.None);

        result.CanSend.Should().BeFalse();
        result.FailureCode.Should().Be("whatsapp_session_expired");
    }

    [Fact]
    public async Task OptOut_ShouldBlock()
    {
        var options = new DbContextOptionsBuilder<FakeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new FakeDbContext(options);

        var tenantId = Guid.NewGuid();
        var contact = new Contact
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = "optout@example.com",
            TagsJson = "[\"optout_email\"]",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var convo = new Conversation { Id = Guid.NewGuid(), TenantId = tenantId, ContactId = contact.Id, PrimaryChannel = Channel.Email, CreatedAt = DateTime.UtcNow };
        db.Contacts.Add(contact);
        db.Conversations.Add(convo);
        await db.SaveChangesAsync();

        var guard = new MessageDispatchGuard(db, Options.Create(new MessagingLimitsOptions()), new FixedTimeProvider(DateTime.UtcNow));

        var job = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ContactId = contact.Id,
            Channel = Channel.Email,
            Type = MessageType.Text,
            PayloadJson = "{}",
            CreatedAt = DateTime.UtcNow
        };

        var result = await guard.EvaluateAsync(job, contact, convo, CancellationToken.None);

        result.CanSend.Should().BeFalse();
        result.FailureCode.Should().Be("opt_out");
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTimeProvider(DateTimeOffset now) => _now = now;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}


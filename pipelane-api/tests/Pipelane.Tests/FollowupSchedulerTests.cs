using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Pipelane.Application.Services;
using Pipelane.Domain.Entities;
using Pipelane.Domain.Enums;
using Pipelane.Infrastructure.Background;

using Xunit;

namespace Pipelane.Tests;

public class FollowupSchedulerTests
{
    private static readonly MethodInfo CreateFollowupsMethod =
        typeof(FollowupScheduler).GetMethod("CreateFollowupTasksAsync", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo EnqueueNudgesMethod =
        typeof(FollowupScheduler).GetMethod("EnqueueNudgesAsync", BindingFlags.NonPublic | BindingFlags.Static)!;

    [Fact]
    public async Task CreateFollowupTasksAsync_AddsTaskWhenOpenedWithoutReply()
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
            Phone = "+33123456789",
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };
        db.Contacts.Add(contact);

        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ContactId = contact.Id,
            PrimaryChannel = Channel.Email,
            CreatedAt = DateTime.UtcNow.AddDays(-5)
        };
        db.Conversations.Add(conversation);

        var staleOpened = new Message
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ConversationId = conversation.Id,
            Channel = Channel.Email,
            Direction = MessageDirection.Out,
            Type = MessageType.Template,
            TemplateName = "welcome",
            Status = MessageStatus.Opened,
            OpenedAt = DateTime.UtcNow.AddHours(-30),
            CreatedAt = DateTime.UtcNow.AddHours(-30)
        };
        db.Messages.Add(staleOpened);

        await db.SaveChangesAsync();

        await InvokeAsync(CreateFollowupsMethod, db, DateTime.UtcNow, CancellationToken.None);

        var task = db.FollowupTasks.Single();
        task.ContactId.Should().Be(contact.Id);
        task.MessageId.Should().Be(staleOpened.Id);
        task.Title.Should().Be("Follow up");
        task.Completed.Should().BeFalse();
        task.DueAtUtc.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task EnqueueNudgesAsync_QueuesTemplateWhenNoResponse()
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
            Phone = "+44700000000",
            CreatedAt = DateTime.UtcNow.AddDays(-12),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };
        db.Contacts.Add(contact);

        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ContactId = contact.Id,
            PrimaryChannel = Channel.Whatsapp,
            CreatedAt = DateTime.UtcNow.AddDays(-6)
        };
        db.Conversations.Add(conversation);

        var outbound = new Message
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ConversationId = conversation.Id,
            Channel = Channel.Whatsapp,
            Direction = MessageDirection.Out,
            Type = MessageType.Template,
            TemplateName = "initial",
            Status = MessageStatus.Sent,
            CreatedAt = DateTime.UtcNow.AddHours(-60)
        };
        db.Messages.Add(outbound);

        var template = new Template
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "nudge-1",
            Channel = Channel.Whatsapp,
            Lang = "en",
            CoreSchemaJson = "{}",
            UpdatedAtUtc = DateTime.UtcNow.AddDays(-1),
            IsActive = true
        };
        db.Templates.Add(template);

        await db.SaveChangesAsync();

        var outboxCapture = new TestOutboxService(db);

        await InvokeAsync(EnqueueNudgesMethod, db, outboxCapture, DateTime.UtcNow, CancellationToken.None);

        outboxCapture.Enqueued.Should().HaveCount(1);
        var enqueued = outboxCapture.Enqueued.Single();
        enqueued.ContactId.Should().Be(contact.Id);
        enqueued.TemplateId.Should().Be(template.Id);
        enqueued.Channel.Should().Be(Channel.Whatsapp);

        db.Outbox.Should().ContainSingle(o => o.Id == enqueued.Id);
    }

    private static async Task InvokeAsync(MethodInfo method, params object?[] parameters)
    {
        var task = (Task)method.Invoke(null, parameters)!;
        await task;
    }

    private sealed class TestOutboxService : IOutboxService
    {
        private readonly FakeDbContext _db;
        public List<OutboxMessage> Enqueued { get; } = new();

        public TestOutboxService(FakeDbContext db) => _db = db;

        public async Task EnqueueAsync(OutboxMessage msg, CancellationToken ct)
        {
            Enqueued.Add(msg);
            await _db.Outbox.AddAsync(msg, ct);
            await _db.SaveChangesAsync(ct);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Pipelane.Application.Abstractions;
using Pipelane.Application.DTOs;
using Pipelane.Application.Services;
using Pipelane.Application.Storage;
using Pipelane.Domain.Entities;
using Pipelane.Domain.Entities.Prospecting;
using Pipelane.Domain.Enums;

using Xunit;

namespace Pipelane.Tests;

public class MessagingServiceTests
{
    private class FakeDb : DbContext, IAppDbContext
    {
        public FakeDb(DbContextOptions<FakeDb> options) : base(options) { }
        public DbSet<Contact> Contacts => Set<Contact>();
        public DbSet<Tenant> Tenants => Set<Tenant>();
        public DbSet<Consent> Consents => Set<Consent>();
        public DbSet<Conversation> Conversations => Set<Conversation>();
        public DbSet<Message> Messages => Set<Message>();
        public DbSet<MessageEvent> MessageEvents => Set<MessageEvent>();
        public DbSet<Template> Templates => Set<Template>();
        public DbSet<Campaign> Campaigns => Set<Campaign>();
        public DbSet<Event> Events => Set<Event>();
        public DbSet<Conversion> Conversions => Set<Conversion>();
        public DbSet<LeadScore> LeadScores => Set<LeadScore>();
        public DbSet<ChannelSettings> ChannelSettings => Set<ChannelSettings>();
        public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();
        public DbSet<User> Users => Set<User>();
        public DbSet<FollowupTask> FollowupTasks => Set<FollowupTask>();
        public DbSet<Prospect> Prospects => Set<Prospect>();
        public DbSet<ProspectingSequence> ProspectingSequences => Set<ProspectingSequence>();
        public DbSet<ProspectingSequenceStep> ProspectingSequenceSteps => Set<ProspectingSequenceStep>();
        public DbSet<ProspectingCampaign> ProspectingCampaigns => Set<ProspectingCampaign>();
        public DbSet<EmailGeneration> EmailGenerations => Set<EmailGeneration>();
        public DbSet<SendLog> ProspectingSendLogs => Set<SendLog>();
        public DbSet<ProspectReply> ProspectReplies => Set<ProspectReply>();
    }

    [Fact]
    public async Task Send_Template_Enqueues_Outbox()
    {
        var options = new DbContextOptionsBuilder<FakeDb>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new FakeDb(options);

        var tenant = Guid.NewGuid();
        var contact = new Contact { Id = Guid.NewGuid(), TenantId = tenant, Phone = "+15550001", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var template = new Template { Id = Guid.NewGuid(), TenantId = tenant, Name = "welcome", Channel = Channel.Whatsapp, Lang = "en", CoreSchemaJson = "{}", IsActive = true, UpdatedAtUtc = DateTime.UtcNow };
        db.Contacts.Add(contact);
        db.Templates.Add(template);
        await db.SaveChangesAsync();

        var svc = new MessagingService(db, new ChannelRegistry(Array.Empty<IMessageChannel>()), new ChannelRulesService(db), new OutboxService(db));
        var res = await svc.SendAsync(new SendMessageRequest(contact.Id, null, Channel.Whatsapp, "template", null, "welcome", "en", new Dictionary<string, string> { { "name", "Alice" } }, null), CancellationToken.None);

        res.Success.Should().BeTrue();
        db.Outbox.Count().Should().Be(1);
    }

    [Fact]
    public async Task Send_Text_Outside_24h_Blocked()
    {
        var options = new DbContextOptionsBuilder<FakeDb>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new FakeDb(options);
        var tenant = Guid.NewGuid();
        var contact = new Contact { Id = Guid.NewGuid(), TenantId = tenant, Phone = "+15550002", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();

        var svc = new MessagingService(db, new ChannelRegistry(Array.Empty<IMessageChannel>()), new ChannelRulesService(db), new OutboxService(db));
        var res = await svc.SendAsync(new SendMessageRequest(contact.Id, null, Channel.Whatsapp, "text", "Hi", null, null, null, null), CancellationToken.None);

        res.Success.Should().BeFalse();
        res.Error.Should().NotBeNull();
    }
}

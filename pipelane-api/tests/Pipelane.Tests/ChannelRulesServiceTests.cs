using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Pipelane.Application.Services;
using Pipelane.Application.Storage;
using Pipelane.Domain.Entities;
using Pipelane.Domain.Entities.Prospecting;
using Pipelane.Domain.Enums;

using Xunit;

namespace Pipelane.Tests;

public class FakeDbContext : DbContext, IAppDbContext
{
    public FakeDbContext(DbContextOptions<FakeDbContext> options) : base(options) { }
    /// <inheritdoc/>
    public DbSet<Contact> Contacts => Set<Contact>();
    /// <inheritdoc/>
    public DbSet<Tenant> Tenants => Set<Tenant>();
    /// <inheritdoc/>
    public DbSet<Consent> Consents => Set<Consent>();
    /// <inheritdoc/>
    public DbSet<Conversation> Conversations => Set<Conversation>();
    /// <inheritdoc/>
    public DbSet<Message> Messages => Set<Message>();
    /// <inheritdoc/>
    public DbSet<MessageEvent> MessageEvents => Set<MessageEvent>();
    /// <inheritdoc/>
    public DbSet<Template> Templates => Set<Template>();
    /// <inheritdoc/>
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    /// <inheritdoc/>
    public DbSet<Event> Events => Set<Event>();
    /// <inheritdoc/>
    public DbSet<Conversion> Conversions => Set<Conversion>();
    /// <inheritdoc/>
    public DbSet<LeadScore> LeadScores => Set<LeadScore>();
    /// <inheritdoc/>
    public DbSet<ChannelSettings> ChannelSettings => Set<ChannelSettings>();
    /// <inheritdoc/>
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();
    /// <inheritdoc/>
    public DbSet<User> Users => Set<User>();
    /// <inheritdoc/>
    public DbSet<FollowupTask> FollowupTasks => Set<FollowupTask>();
    /// <inheritdoc/>
    public DbSet<Prospect> Prospects => Set<Prospect>();
    /// <inheritdoc/>
    public DbSet<ProspectingSequence> ProspectingSequences => Set<ProspectingSequence>();
    /// <inheritdoc/>
    public DbSet<ProspectingSequenceStep> ProspectingSequenceSteps => Set<ProspectingSequenceStep>();
    /// <inheritdoc/>
    public DbSet<ProspectingCampaign> ProspectingCampaigns => Set<ProspectingCampaign>();
    /// <inheritdoc/>
    public DbSet<EmailGeneration> EmailGenerations => Set<EmailGeneration>();
    /// <inheritdoc/>
    public DbSet<SendLog> ProspectingSendLogs => Set<SendLog>();
    /// <inheritdoc/>
    public DbSet<ProspectReply> ProspectReplies => Set<ProspectReply>();
    /// <inheritdoc/>
    public DbSet<RateLimitSnapshot> RateLimitSnapshots => Set<RateLimitSnapshot>();
    /// <inheritdoc/>
    public DbSet<FailedWebhook> FailedWebhooks => Set<FailedWebhook>();
    /// <inheritdoc/>
    public DbSet<ProspectList> ProspectLists => Set<ProspectList>();
    /// <inheritdoc/>
    public DbSet<ProspectListItem> ProspectListItems => Set<ProspectListItem>();
    /// <inheritdoc/>
    public DbSet<ProspectScore> ProspectScores => Set<ProspectScore>();
}

public class ChannelRulesServiceTests
{
    [Fact]
    public async Task WhatsappSession_Allows_Within_24h()
    {
        var options = new DbContextOptionsBuilder<FakeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new FakeDbContext(options);

        var contactId = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var convo = new Conversation { Id = Guid.NewGuid(), TenantId = tenant, ContactId = contactId, PrimaryChannel = Channel.Whatsapp, CreatedAt = DateTime.UtcNow };
        db.Conversations.Add(convo);
        db.Messages.Add(new Message { Id = Guid.NewGuid(), TenantId = tenant, ConversationId = convo.Id, Channel = Channel.Whatsapp, Direction = MessageDirection.In, Type = MessageType.Text, CreatedAt = DateTime.UtcNow.AddHours(-1) });
        await db.SaveChangesAsync();

        var svc = new ChannelRulesService(db);
        var ok = await svc.CanSendWhatsAppSessionAsync(contactId, CancellationToken.None);
        ok.Should().BeTrue();
    }

    [Fact]
    public async Task WhatsappSession_Blocks_After_24h()
    {
        var options = new DbContextOptionsBuilder<FakeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new FakeDbContext(options);

        var contactId = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var convo = new Conversation { Id = Guid.NewGuid(), TenantId = tenant, ContactId = contactId, PrimaryChannel = Channel.Whatsapp, CreatedAt = DateTime.UtcNow };
        db.Conversations.Add(convo);
        db.Messages.Add(new Message { Id = Guid.NewGuid(), TenantId = tenant, ConversationId = convo.Id, Channel = Channel.Whatsapp, Direction = MessageDirection.In, Type = MessageType.Text, CreatedAt = DateTime.UtcNow.AddHours(-25) });
        await db.SaveChangesAsync();

        var svc = new ChannelRulesService(db);
        var ok = await svc.CanSendWhatsAppSessionAsync(contactId, CancellationToken.None);
        ok.Should().BeFalse();
    }
}

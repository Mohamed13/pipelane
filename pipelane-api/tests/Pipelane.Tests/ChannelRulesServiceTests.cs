using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Pipelane.Application.Services;
using Pipelane.Application.Storage;
using Pipelane.Domain.Entities;
using Pipelane.Domain.Enums;

using Xunit;

namespace Pipelane.Tests;

public class FakeDbContext : DbContext, IAppDbContext
{
    public FakeDbContext(DbContextOptions<FakeDbContext> options) : base(options) { }
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

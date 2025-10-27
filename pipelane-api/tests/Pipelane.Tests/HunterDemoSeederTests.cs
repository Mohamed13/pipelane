using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Pipelane.Application.Services;
using Pipelane.Application.Storage;
using Pipelane.Domain.Entities;
using Pipelane.Domain.Entities.Prospecting;
using Pipelane.Infrastructure.Demo;
using Xunit;

namespace Pipelane.Tests;

public class HunterDemoSeederTests
{
    private sealed class TestDbContext : DbContext, IAppDbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

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
        public DbSet<ProspectList> ProspectLists => Set<ProspectList>();
        public DbSet<ProspectListItem> ProspectListItems => Set<ProspectListItem>();
        public DbSet<ProspectScore> ProspectScores => Set<ProspectScore>();
        public DbSet<RateLimitSnapshot> RateLimitSnapshots => Set<RateLimitSnapshot>();
        public DbSet<FailedWebhook> FailedWebhooks => Set<FailedWebhook>();
    }

    [Fact]
    public async Task SeedAsync_creates_50_demo_prospects_and_returns_results()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new TestDbContext(options);
        var seeder = new HunterDemoSeeder(db, TimeProvider.System, Options.Create(new DemoOptions { Enabled = true }), NullLogger<HunterDemoSeeder>.Instance);
        var tenantId = Guid.NewGuid();

        var results = await seeder.SeedAsync(tenantId, CancellationToken.None);

        results.Should().HaveCount(50);
        db.Prospects.Count().Should().Be(50);
        db.ProspectScores.Count().Should().Be(50);
        results.Select(r => r.Prospect.Company).Distinct().Count().Should().BeGreaterOrEqualTo(45);
    }

    [Fact]
    public async Task SeedAsync_resets_previous_demo_entries()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new TestDbContext(options);
        var seeder = new HunterDemoSeeder(db, TimeProvider.System, Options.Create(new DemoOptions { Enabled = true }), NullLogger<HunterDemoSeeder>.Instance);
        var tenantId = Guid.NewGuid();

        await seeder.SeedAsync(tenantId, CancellationToken.None);
        await seeder.SeedAsync(tenantId, CancellationToken.None);

        db.Prospects.Count().Should().Be(50);
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using Pipelane.Application.Hunter;
using Pipelane.Application.Services;
using Pipelane.Application.Storage;
using Pipelane.Domain.Entities;
using Pipelane.Domain.Entities.Prospecting;
using Pipelane.Domain.Enums.Prospecting;

using Xunit;

namespace Pipelane.Tests;

public class HunterServiceTests
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
        public DbSet<RateLimitSnapshot> RateLimitSnapshots => Set<RateLimitSnapshot>();
        public DbSet<FailedWebhook> FailedWebhooks => Set<FailedWebhook>();
        public DbSet<ProspectList> ProspectLists => Set<ProspectList>();
        public DbSet<ProspectListItem> ProspectListItems => Set<ProspectListItem>();
        public DbSet<ProspectScore> ProspectScores => Set<ProspectScore>();
    }

    private sealed class InMemoryCsvStore : IHunterCsvStore
    {
        private readonly Dictionary<Guid, string> _storage = new();

        public Task DeleteAsync(Guid tenantId, Guid csvId, CancellationToken ct)
        {
            _storage.Remove(csvId);
            return Task.CompletedTask;
        }

        public Task<Stream> OpenAsync(Guid tenantId, Guid csvId, CancellationToken ct)
        {
            var content = _storage[csvId];
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
            return Task.FromResult<Stream>(stream);
        }

        public Task<Guid> SaveAsync(Guid tenantId, Stream content, CancellationToken ct)
        {
            using var reader = new StreamReader(content, leaveOpen: true);
            var text = reader.ReadToEnd();
            var id = Guid.NewGuid();
            _storage[id] = text;
            content.Position = 0;
            return Task.FromResult(id);
        }
    }

    private sealed class ConstantLimitsProvider : IMessagingLimitsProvider
    {
        public MessagingLimitsSnapshot GetLimits() => new(100, TimeSpan.FromHours(22), TimeSpan.FromHours(8));
    }

    private static HunterService CreateService(TestDbContext db, IEnumerable<ILeadProvider> providers)
    {
        return new HunterService(
            db,
            providers,
            new HunterEnrichService(NullLogger<HunterEnrichService>.Instance),
            new HunterScoreService(),
            new WhyThisLeadService(),
            new InMemoryCsvStore(),
            new ConstantLimitsProvider(),
            NullLogger<HunterService>.Instance);
    }

    [Fact]
    public async Task Hunter_Search_Returns_Scored_Results()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new TestDbContext(options);
        var tenantId = Guid.NewGuid();
        var maps = new MapsStubLeadProvider(NullLogger<MapsStubLeadProvider>.Instance);
        var service = CreateService(db, new ILeadProvider[] { maps });

        var criteria = new HunterSearchCriteria("Restaurants", new GeoCriteria(48.85, 2.35, 5), null, "mapsStub", string.Empty, null);
        var result = await service.SearchAsync(tenantId, criteria, dryRun: false, CancellationToken.None);

        result.Total.Should().Be(30);
        result.Duplicates.Should().Be(0);
        result.Items.Should().HaveCount(30);
        result.Items.Should().OnlyContain(item => item.Score >= 0 && item.Score <= 100);

        db.Prospects.Count().Should().Be(30);
        db.ProspectScores.Count().Should().Be(30);
    }

    [Fact]
    public async Task Dedupe_Skips_Existing_ByEmail_OrFuzzy()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new TestDbContext(options);
        var tenantId = Guid.NewGuid();
        var existingProspect = new Prospect
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = "existing@example.com",
            Company = "Acme",
            City = "Paris",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        await db.Prospects.AddAsync(existingProspect);
        await db.SaveChangesAsync();

        var provider = new TestLeadProvider();
        var service = CreateService(db, new ILeadProvider[] { provider });
        var criteria = new HunterSearchCriteria(null, null, null, "test", null, null);

        var result = await service.SearchAsync(tenantId, criteria, dryRun: false, CancellationToken.None);

        result.Total.Should().Be(3);
        result.Duplicates.Should().Be(2);
        db.Prospects.Count().Should().Be(2); // existing + unique new
    }

    [Fact]
    public void ScoreService_Ponds_Are_Bounded_0_100()
    {
        var score = new HunterScoreService();
        var featuresHigh = new HunterFeaturesDto(4.9, 800, true, true, true, "WordPress", true, true, false);
        var featuresLow = new HunterFeaturesDto(2.1, 3, false, false, false, null, false, false, true);

        var high = score.ComputeScore(featuresHigh, null);
        var low = score.ComputeScore(featuresLow, null);

        high.Should().BeGreaterOrEqualTo(0).And.BeLessOrEqualTo(100);
        low.Should().BeGreaterOrEqualTo(0).And.BeLessOrEqualTo(100);
        high.Should().BeGreaterThan(low);
    }

    [Fact]
    public async Task Lists_AddAndGet_Works()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new TestDbContext(options);
        var tenantId = Guid.NewGuid();
        var service = CreateService(db, new ILeadProvider[] { new TestLeadProvider() });

        var prospect = new Prospect
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = "unique@example.com",
            Company = "Beta",
            City = "Lyon",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        await db.Prospects.AddAsync(prospect);
        await db.ProspectScores.AddAsync(new ProspectScore
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProspectId = prospect.Id,
            Score = 75,
            FeaturesJson = JsonSerializer.Serialize(new
            {
                enriched = new HunterFeaturesDto(4, 120, true, true, true, "WordPress", true, true, false),
                why = new[] { "Profil cohérent" }
            }),
            UpdatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var listId = await service.CreateListAsync(tenantId, new CreateListRequest("VIP"), CancellationToken.None);
        var add = await service.AddToListAsync(tenantId, listId, new AddToListRequest(new[] { prospect.Id }), CancellationToken.None);
        add.Added.Should().Be(1);
        add.Skipped.Should().Be(0);

        var detail = await service.GetListAsync(tenantId, listId, CancellationToken.None);
        detail.Items.Should().NotBeNull();
        detail.Items!.Should().ContainSingle();
        detail.Items!.First().Score.Should().Be(75);
    }

    [Fact]
    public async Task Lists_Get_Summaries()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new TestDbContext(options);
        var tenantId = Guid.NewGuid();
        var service = CreateService(db, new ILeadProvider[] { new TestLeadProvider() });

        var list = new ProspectList
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Demo",
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-5)
        };
        await db.ProspectLists.AddAsync(list);
        await db.ProspectListItems.AddAsync(new ProspectListItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProspectListId = list.Id,
            ProspectId = Guid.NewGuid(),
            AddedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var summaries = await service.GetListsAsync(tenantId, CancellationToken.None);
        summaries.Should().ContainSingle();
        summaries[0].Name.Should().Be("Demo");
        summaries[0].Count.Should().Be(1);
    }

    private sealed class TestLeadProvider : ILeadProvider
    {
        public string Source => "test";

        public Task<IReadOnlyList<LeadCandidate>> SearchAsync(Guid tenantId, HunterSearchCriteria criteria, CancellationToken ct)
        {
            var leads = new List<LeadCandidate>
            {
                new(new HunterProspectDto("Anna", "Martin", "Acme", "existing@example.com", null, null, null, "Paris", "France", null),
                    new HunterFeaturesDto(4, 100, true, true, true, null, true, false, false)),
                new(new HunterProspectDto("Paul", "Durand", "Acme", null, null, null, null, "Paris", "France", null),
                    new HunterFeaturesDto(3.5, 80, true, false, false, null, true, false, false)),
                new(new HunterProspectDto("Zoé", "Lemoine", "Beta", "beta@example.com", null, null, null, "Lyon", "France", null),
                    new HunterFeaturesDto(4.2, 120, true, true, true, null, true, true, false))
            };
            return Task.FromResult<IReadOnlyList<LeadCandidate>>(leads);
        }
    }
}

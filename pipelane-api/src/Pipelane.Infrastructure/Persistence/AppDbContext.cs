using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

using Pipelane.Application.Storage;
using Pipelane.Domain.Entities;
using Pipelane.Domain.Entities.Prospecting;

namespace Pipelane.Infrastructure.Persistence;

public class AppDbContext : DbContext, IAppDbContext, IDatabaseDiagnostics
{
    private readonly Guid _tenantId;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantProvider tenantProvider) : base(options)
    {
        _tenantId = tenantProvider.TenantId;
    }

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
    public DbSet<ProspectList> ProspectLists => Set<ProspectList>();

    /// <inheritdoc/>
    public DbSet<ProspectListItem> ProspectListItems => Set<ProspectListItem>();

    /// <inheritdoc/>
    public DbSet<ProspectScore> ProspectScores => Set<ProspectScore>();

    /// <inheritdoc/>
    public DbSet<RateLimitSnapshot> RateLimitSnapshots => Set<RateLimitSnapshot>();

    /// <inheritdoc/>
    public DbSet<FailedWebhook> FailedWebhooks => Set<FailedWebhook>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Use default schema for provider (dbo on SQL Server)
        // Global query filters for TenantId
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(AppDbContext).GetMethod(nameof(ApplyTenantFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .MakeGenericMethod(entityType.ClrType);
                method.Invoke(this, new object[] { modelBuilder });
            }
        }

        // Indexes
        modelBuilder.Entity<Message>()
            .HasIndex(m => new { m.ConversationId, m.CreatedAt })
            .HasDatabaseName("IX_messages_conversation_created");
        modelBuilder.Entity<Message>()
            .HasIndex(m => new { m.TenantId, m.Channel, m.CreatedAt });
        modelBuilder.Entity<Message>()
            .HasIndex(m => new { m.TenantId, m.ProviderMessageId })
            .IsUnique()
            .HasFilter("[ProviderMessageId] IS NOT NULL")
            .HasDatabaseName("IX_Messages_TenantId_ProviderMessageId");
        modelBuilder.Entity<Message>()
            .HasMany(m => m.Events)
            .WithOne(e => e.Message!)
            .HasForeignKey(e => e.MessageId);

        modelBuilder.Entity<MessageEvent>()
            .Property(e => e.Provider)
            .HasMaxLength(128);
        modelBuilder.Entity<MessageEvent>()
            .Property(e => e.ProviderEventId)
            .HasMaxLength(128);
        modelBuilder.Entity<MessageEvent>()
            .HasIndex(e => new { e.TenantId, e.MessageId, e.CreatedAt })
            .HasDatabaseName("IX_MessageEvents_Tenant_Message_Created");
        modelBuilder.Entity<MessageEvent>()
            .HasIndex(e => new { e.Provider, e.ProviderEventId })
            .IsUnique()
            .HasFilter("[ProviderEventId] IS NOT NULL")
            .HasDatabaseName("IX_MessageEvents_Provider_EventId");

        modelBuilder.Entity<FollowupTask>()
            .HasIndex(t => new { t.TenantId, t.ContactId, t.Completed })
            .HasDatabaseName("IX_FollowupTasks_Tenant_Contact_Completed");
        modelBuilder.Entity<FollowupTask>()
            .HasIndex(t => t.MessageId)
            .IsUnique()
            .HasFilter("[MessageId] IS NOT NULL")
            .HasDatabaseName("IX_FollowupTasks_Message");

        modelBuilder.Entity<Contact>()
            .HasIndex(c => new { c.TenantId, c.Phone })
            .IsUnique();
        modelBuilder.Entity<Contact>()
            .HasIndex(c => new { c.TenantId, c.Email })
            .IsUnique();

        modelBuilder.Entity<Template>()
            .HasIndex(t => new { t.TenantId, t.Name, t.Lang, t.Channel })
            .IsUnique();

        modelBuilder.Entity<Campaign>()
            .HasIndex(c => new { c.TenantId, c.ScheduledAtUtc })
            .HasDatabaseName("IX_campaigns_tenant_scheduled");

        modelBuilder.Entity<Event>()
            .HasIndex(e => new { e.TenantId, e.CreatedAt })
            .HasDatabaseName("IX_events_tenant_created");

        modelBuilder.Entity<Consent>()
            .HasIndex(c => new { c.ContactId, c.Channel })
            .IsUnique()
            .HasDatabaseName("IX_consents_contact_channel");

        modelBuilder.Entity<Conversation>()
            .HasIndex(c => new { c.TenantId, c.ContactId });
        modelBuilder.Entity<Conversation>()
            .HasIndex(c => new { c.TenantId, c.ProviderThreadId });

        modelBuilder.Entity<ChannelSettings>()
            .HasIndex(c => new { c.TenantId, c.Channel })
            .IsUnique();

        modelBuilder.Entity<LeadScore>()
            .HasIndex(l => new { l.TenantId, l.ContactId })
            .IsUnique()
            .HasFilter("[ContactId] IS NOT NULL")
            .HasDatabaseName("IX_LeadScores_Tenant_Contact");
        modelBuilder.Entity<LeadScore>()
            .HasIndex(l => new { l.TenantId, l.ProspectId })
            .IsUnique()
            .HasFilter("[ProspectId] IS NOT NULL")
            .HasDatabaseName("IX_LeadScores_Tenant_Prospect");

        modelBuilder.Entity<Conversion>()
            .Property(c => c.Amount)
            .HasPrecision(18, 2);
        modelBuilder.Entity<Conversion>()
            .HasIndex(c => new { c.TenantId, c.ContactId, c.RevenueAtUtc });
        modelBuilder.Entity<Conversion>()
            .HasIndex(c => new { c.TenantId, c.CampaignId, c.RevenueAtUtc });

        modelBuilder.Entity<OutboxMessage>()
            .HasIndex(o => new { o.Status, o.ScheduledAtUtc, o.LockedUntilUtc, o.CreatedAt });

        modelBuilder.Entity<User>()
            .HasIndex(u => new { u.TenantId, u.Email })
            .IsUnique();
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email);

        modelBuilder.Entity<Prospect>()
            .Property(p => p.Email)
            .HasMaxLength(320);
        modelBuilder.Entity<Prospect>()
            .Property(p => p.City)
            .HasMaxLength(128);
        modelBuilder.Entity<Prospect>()
            .Property(p => p.Country)
            .HasMaxLength(128);
        modelBuilder.Entity<Prospect>()
            .Property(p => p.Website)
            .HasMaxLength(512);
        modelBuilder.Entity<Prospect>()
            .HasIndex(p => new { p.TenantId, p.Email })
            .IsUnique()
            .HasDatabaseName("IX_Prospects_Tenant_Email");
        modelBuilder.Entity<Prospect>()
            .HasIndex(p => new { p.TenantId, p.Status })
            .HasDatabaseName("IX_Prospects_Tenant_Status");
        modelBuilder.Entity<Prospect>()
            .HasIndex(p => new { p.TenantId, p.OwnerUserId })
            .HasDatabaseName("IX_Prospects_Tenant_Owner");
        modelBuilder.Entity<Prospect>()
            .HasIndex(p => new { p.TenantId, p.City })
            .HasDatabaseName("IX_Prospects_Tenant_City");
        modelBuilder.Entity<Prospect>()
            .HasIndex(p => new { p.TenantId, p.Company })
            .HasDatabaseName("IX_Prospects_Tenant_Company");
        modelBuilder.Entity<Prospect>()
            .HasOne(p => p.Sequence)
            .WithMany(s => s.Prospects)
            .HasForeignKey(p => p.SequenceId)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Prospect>()
            .HasOne(p => p.Campaign)
            .WithMany(c => c.Prospects)
            .HasForeignKey(p => p.CampaignId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ProspectingSequence>()
            .HasIndex(s => new { s.TenantId, s.Name })
            .IsUnique()
            .HasDatabaseName("IX_ProspectingSequences_Tenant_Name");

        modelBuilder.Entity<ProspectingSequenceStep>()
            .HasIndex(s => new { s.TenantId, s.SequenceId, s.Order })
            .IsUnique()
            .HasDatabaseName("IX_ProspectingSteps_Sequence_Order");
        modelBuilder.Entity<ProspectingSequenceStep>()
            .HasOne(s => s.Sequence)
            .WithMany(seq => seq.Steps)
            .HasForeignKey(s => s.SequenceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProspectingCampaign>()
            .HasIndex(c => new { c.TenantId, c.Status })
            .HasDatabaseName("IX_ProspectingCampaigns_Tenant_Status");
        modelBuilder.Entity<ProspectingCampaign>()
            .HasOne(c => c.Sequence)
            .WithMany(s => s.Campaigns)
            .HasForeignKey(c => c.SequenceId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProspectList>()
            .HasIndex(l => new { l.TenantId, l.Name })
            .IsUnique()
            .HasDatabaseName("IX_ProspectLists_Tenant_Name");
        modelBuilder.Entity<ProspectList>()
            .Property(l => l.Name)
            .HasMaxLength(200);

        modelBuilder.Entity<ProspectListItem>()
            .HasIndex(i => new { i.TenantId, i.ProspectListId, i.ProspectId })
            .IsUnique()
            .HasDatabaseName("IX_ProspectListItems_List_Prospect");
        modelBuilder.Entity<ProspectListItem>()
            .HasOne(i => i.List)
            .WithMany(l => l.Items)
            .HasForeignKey(i => i.ProspectListId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ProspectListItem>()
            .HasOne(i => i.Prospect)
            .WithMany(p => p.ListItems)
            .HasForeignKey(i => i.ProspectId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProspectScore>()
            .HasIndex(s => new { s.TenantId, s.ProspectId })
            .IsUnique()
            .HasDatabaseName("IX_ProspectScores_Tenant_Prospect");
        modelBuilder.Entity<ProspectScore>()
            .Property(s => s.FeaturesJson)
            .HasColumnType("nvarchar(max)");
        modelBuilder.Entity<ProspectScore>()
            .HasOne(s => s.Prospect)
            .WithOne(p => p.Score)
            .HasForeignKey<ProspectScore>(s => s.ProspectId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<EmailGeneration>()
            .HasIndex(g => new { g.TenantId, g.ProspectId, g.StepId, g.CreatedAtUtc })
            .HasDatabaseName("IX_EmailGenerations_Prospect_Step_Created");
        modelBuilder.Entity<EmailGeneration>()
            .Property(g => g.Variant)
            .HasMaxLength(16);
        modelBuilder.Entity<EmailGeneration>()
            .Property(g => g.Temperature)
            .HasPrecision(4, 3);
        modelBuilder.Entity<EmailGeneration>()
            .Property(g => g.CostUsd)
            .HasPrecision(18, 6);
        modelBuilder.Entity<EmailGeneration>()
            .HasOne(g => g.Prospect)
            .WithMany(p => p.Generations)
            .HasForeignKey(g => g.ProspectId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<EmailGeneration>()
            .HasOne(g => g.Step)
            .WithMany(s => s.Generations)
            .HasForeignKey(g => g.StepId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<EmailGeneration>()
            .HasOne(g => g.Campaign)
            .WithMany(c => c.Generations)
            .HasForeignKey(g => g.CampaignId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<SendLog>()
            .HasIndex(s => new { s.TenantId, s.ProspectId, s.Status, s.ScheduledAtUtc })
            .HasDatabaseName("IX_SendLogs_Tenant_Prospect_Status_Scheduled");
        modelBuilder.Entity<SendLog>()
            .Property(s => s.Provider)
            .HasMaxLength(128);
        modelBuilder.Entity<SendLog>()
            .HasIndex(s => new { s.TenantId, s.Provider, s.ProviderMessageId })
            .IsUnique()
            .HasFilter("[ProviderMessageId] IS NOT NULL")
            .HasDatabaseName("IX_SendLogs_ProviderMessage");
        modelBuilder.Entity<SendLog>()
            .HasOne(s => s.Prospect)
            .WithMany(p => p.Sends)
            .HasForeignKey(s => s.ProspectId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SendLog>()
            .HasOne(s => s.Campaign)
            .WithMany(c => c.Sends)
            .HasForeignKey(s => s.CampaignId)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<SendLog>()
            .HasOne(s => s.Step)
            .WithMany(step => step.Sends)
            .HasForeignKey(s => s.StepId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SendLog>()
            .HasOne(s => s.Generation)
            .WithMany(g => g.Sends)
            .HasForeignKey(s => s.GenerationId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ProspectReply>()
            .HasIndex(r => new { r.TenantId, r.ProspectId, r.ReceivedAtUtc })
            .HasDatabaseName("IX_ProspectReplies_Tenant_Prospect_Received");
        modelBuilder.Entity<ProspectReply>()
            .HasOne(r => r.Prospect)
            .WithMany(p => p.Replies)
            .HasForeignKey(r => r.ProspectId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ProspectReply>()
            .HasOne(r => r.Campaign)
            .WithMany(c => c.Replies)
            .HasForeignKey(r => r.CampaignId)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<ProspectReply>()
            .HasOne(r => r.SendLog)
            .WithMany(s => s.Replies)
            .HasForeignKey(r => r.SendLogId)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<ProspectReply>()
            .HasOne(r => r.Step)
            .WithMany(s => s.Replies)
            .HasForeignKey(r => r.StepId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ProspectReply>()
            .HasOne(r => r.AutoReplyGeneration)
            .WithMany()
            .HasForeignKey(r => r.AutoReplyGenerationId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<RateLimitSnapshot>()
            .HasIndex(r => new { r.TargetTenantId, r.Scope })
            .IsUnique()
            .HasDatabaseName("IX_RateLimitSnapshots_Target_Scope");
        modelBuilder.Entity<RateLimitSnapshot>()
            .Property(r => r.Scope)
            .HasMaxLength(64);

        modelBuilder.Entity<FailedWebhook>()
            .HasIndex(f => new { f.TenantId, f.NextAttemptUtc, f.RetryCount })
            .HasDatabaseName("IX_FailedWebhooks_Tenant_NextAttempt");
        modelBuilder.Entity<FailedWebhook>()
            .Property(f => f.Provider)
            .HasMaxLength(128);
        modelBuilder.Entity<FailedWebhook>()
            .Property(f => f.Kind)
            .HasMaxLength(128);
        modelBuilder.Entity<FailedWebhook>()
            .Property(f => f.Payload)
            .HasColumnType("nvarchar(max)");
        modelBuilder.Entity<FailedWebhook>()
            .Property(f => f.HeadersJson)
            .HasColumnType("nvarchar(max)");
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : BaseEntity
        => modelBuilder.Entity<TEntity>().HasQueryFilter(e => _tenantId == Guid.Empty || e.TenantId == _tenantId);

    /// <inheritdoc/>
    public string ProviderName => Database.ProviderName ?? "unknown";

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetPendingMigrationsAsync(CancellationToken ct)
    {
        try
        {
            var pending = await Database.GetPendingMigrationsAsync(ct);
            return pending.ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}

public interface ITenantProvider
{
    Guid TenantId { get; }
}

public interface IDatabaseDiagnostics
{
    string ProviderName { get; }
    Task<IReadOnlyList<string>> GetPendingMigrationsAsync(CancellationToken ct);
}

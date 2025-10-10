using Microsoft.EntityFrameworkCore;

using Pipelane.Application.Storage;
using Pipelane.Domain.Entities;

namespace Pipelane.Infrastructure.Persistence;

public class AppDbContext : DbContext, IAppDbContext
{
    private readonly Guid _tenantId;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantProvider tenantProvider) : base(options)
    {
        _tenantId = tenantProvider.TenantId;
    }

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
            .IsUnique();

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
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : BaseEntity
        => modelBuilder.Entity<TEntity>().HasQueryFilter(e => _tenantId == Guid.Empty || e.TenantId == _tenantId);
}

public interface ITenantProvider
{
    Guid TenantId { get; }
}

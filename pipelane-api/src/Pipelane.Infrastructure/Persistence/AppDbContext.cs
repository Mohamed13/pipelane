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
    public DbSet<Template> Templates => Set<Template>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Conversion> Conversions => Set<Conversion>();
    public DbSet<LeadScore> LeadScores => Set<LeadScore>();
    public DbSet<ChannelSettings> ChannelSettings => Set<ChannelSettings>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("public");

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

        modelBuilder.Entity<Contact>()
            .HasIndex(c => new { c.TenantId, c.Phone })
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
            .HasDatabaseName("IX_consents_contact_channel");
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : BaseEntity
        => modelBuilder.Entity<TEntity>().HasQueryFilter(e => _tenantId == Guid.Empty || e.TenantId == _tenantId);
}

public interface ITenantProvider
{
    Guid TenantId { get; }
}

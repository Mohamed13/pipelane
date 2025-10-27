using System.Linq;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Pipelane.Domain.Entities;
using Pipelane.Infrastructure.Persistence;

namespace Pipelane.Infrastructure.Background;

public interface IRateLimitSnapshotStore
{
    IReadOnlyCollection<DateTime> Load(Guid targetTenantId);
    void Save(Guid targetTenantId, IReadOnlyCollection<DateTime> hits);
}

public sealed class RateLimitSnapshotStore : IRateLimitSnapshotStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RateLimitSnapshotStore> _logger;

    public RateLimitSnapshotStore(IServiceScopeFactory scopeFactory, ILogger<RateLimitSnapshotStore> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public IReadOnlyCollection<DateTime> Load(Guid targetTenantId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var snapshot = db.RateLimitSnapshots
            .AsNoTracking()
            .FirstOrDefault(r => r.TargetTenantId == targetTenantId && r.Scope == "send");

        if (snapshot is null || string.IsNullOrWhiteSpace(snapshot.HitsJson))
        {
            return Array.Empty<DateTime>();
        }

        try
        {
            var data = JsonSerializer.Deserialize<List<DateTime>>(snapshot.HitsJson, SerializerOptions);
            if (data is null || data.Count == 0)
            {
                return Array.Empty<DateTime>();
            }
            data.Sort();
            return data;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize rate limit snapshot for tenant {TenantId}", targetTenantId);
            return Array.Empty<DateTime>();
        }
    }

    public void Save(Guid targetTenantId, IReadOnlyCollection<DateTime> hits)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var snapshot = db.RateLimitSnapshots
            .FirstOrDefault(r => r.TargetTenantId == targetTenantId && r.Scope == "send");

        if (snapshot is null)
        {
            snapshot = new RateLimitSnapshot
            {
                Id = Guid.NewGuid(),
                TenantId = Guid.Empty,
                TargetTenantId = targetTenantId,
                Scope = "send"
            };
            db.RateLimitSnapshots.Add(snapshot);
        }

        snapshot.HitsJson = JsonSerializer.Serialize(hits, SerializerOptions);
        snapshot.WindowStartUtc = hits.Count > 0 ? hits.Min() : DateTime.UtcNow;
        snapshot.UpdatedAtUtc = DateTime.UtcNow;

        db.SaveChanges();
    }
}

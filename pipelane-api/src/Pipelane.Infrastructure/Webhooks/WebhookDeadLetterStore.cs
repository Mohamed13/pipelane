using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Pipelane.Domain.Entities;
using Pipelane.Domain.Enums;
using Pipelane.Infrastructure.Persistence;

namespace Pipelane.Infrastructure.Webhooks;

public interface IWebhookDeadLetterStore
{
    Task LogFailureAsync(Guid tenantId, Channel channel, string provider, string kind, string payload, IDictionary<string, string> headers, string error, CancellationToken ct);
    Task<IReadOnlyList<WebhookDeadLetterItem>> TakeDueAsync(DateTime utcNow, int batchSize, CancellationToken ct);
    Task MarkSuccessAsync(Guid id, CancellationToken ct);
    Task MarkFailureAsync(Guid id, string error, TimeSpan backoff, CancellationToken ct);
}

public sealed record WebhookDeadLetterItem(
    Guid Id,
    Guid TenantId,
    Channel Channel,
    string Provider,
    string Kind,
    string Payload,
    IReadOnlyDictionary<string, string> Headers,
    int RetryCount);

public sealed class WebhookDeadLetterStore : IWebhookDeadLetterStore
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WebhookDeadLetterStore> _logger;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public WebhookDeadLetterStore(IServiceScopeFactory scopeFactory, ILogger<WebhookDeadLetterStore> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task LogFailureAsync(Guid tenantId, Channel channel, string provider, string kind, string payload, IDictionary<string, string> headers, string error, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var entity = new FailedWebhook
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Channel = channel,
            Provider = provider,
            Kind = kind,
            Payload = payload,
            HeadersJson = JsonSerializer.Serialize(headers, SerializerOptions),
            LastError = error,
            RetryCount = 0,
            NextAttemptUtc = DateTime.UtcNow.AddMinutes(2),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        db.FailedWebhooks.Add(entity);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<WebhookDeadLetterItem>> TakeDueAsync(DateTime utcNow, int batchSize, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var items = await db.FailedWebhooks
            .Where(f => f.NextAttemptUtc <= utcNow)
            .OrderBy(f => f.NextAttemptUtc)
            .Take(batchSize)
            .AsNoTracking()
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return items.Select(item =>
        {
            var headers = SafeDeserializeHeaders(item.HeadersJson);
            return new WebhookDeadLetterItem(item.Id, item.TenantId, item.Channel, item.Provider, item.Kind, item.Payload, headers, item.RetryCount);
        }).ToList();
    }

    public async Task MarkSuccessAsync(Guid id, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var entity = await db.FailedWebhooks.FirstOrDefaultAsync(f => f.Id == id, ct).ConfigureAwait(false);
        if (entity is null)
        {
            return;
        }

        db.FailedWebhooks.Remove(entity);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task MarkFailureAsync(Guid id, string error, TimeSpan backoff, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var entity = await db.FailedWebhooks.FirstOrDefaultAsync(f => f.Id == id, ct).ConfigureAwait(false);
        if (entity is null)
        {
            _logger.LogWarning("Dead-letter webhook {WebhookId} missing when marking failure", id);
            return;
        }

        entity.RetryCount += 1;
        entity.LastError = error;
        entity.NextAttemptUtc = DateTime.UtcNow.Add(backoff);
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private static IReadOnlyDictionary<string, string> SafeDeserializeHeaders(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var dictionary = JsonSerializer.Deserialize<Dictionary<string, string>>(json, SerializerOptions);
            return dictionary is null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(dictionary, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }
}

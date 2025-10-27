using System.Linq;

using Microsoft.Extensions.Options;

namespace Pipelane.Infrastructure.Background;

public interface IMessageSendRateLimiter
{
    Task<bool> TryAcquireAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

public sealed class MessageSendRateLimiter : IMessageSendRateLimiter
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid, Queue<DateTime>> _queues = new();
    private readonly HashSet<Guid> _dirtyTenants = new();
    private readonly TimeSpan _window = TimeSpan.FromMinutes(1);
    private readonly int _globalLimit;
    private readonly int _tenantLimit;
    private readonly TimeProvider _clock;
    private readonly IRateLimitSnapshotStore _store;

    public MessageSendRateLimiter(
        IOptions<MessagingLimitsOptions> options,
        IRateLimitSnapshotStore store,
        TimeProvider? clock = null)
    {
        var value = options.Value;
        _globalLimit = Math.Max(1, value.PerMinuteGlobal);
        _tenantLimit = Math.Max(1, value.PerMinutePerTenant);
        _clock = clock ?? TimeProvider.System;
        _store = store;
    }

    public Task<bool> TryAcquireAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(false);
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var key = tenantId == Guid.Empty ? Guid.Empty : tenantId;

        var globalQueue = EnsureQueue(Guid.Empty);
        var tenantQueue = key == Guid.Empty ? globalQueue : EnsureQueue(key);

        lock (_sync)
        {
            TrimQueue(globalQueue, now);
            if (globalQueue.Count >= _globalLimit)
            {
                return Task.FromResult(false);
            }

            TrimQueue(tenantQueue, now);
            if (tenantQueue.Count >= _tenantLimit)
            {
                return Task.FromResult(false);
            }

            globalQueue.Enqueue(now);
            tenantQueue.Enqueue(now);
            _dirtyTenants.Add(Guid.Empty);
            _dirtyTenants.Add(key);
        }

        PersistDirtyQueues();
        return Task.FromResult(true);
    }

    private Queue<DateTime> EnsureQueue(Guid key)
    {
        lock (_sync)
        {
            if (_queues.TryGetValue(key, out var existing))
            {
                return existing;
            }
        }

        var loaded = _store.Load(key);
        var newQueue = new Queue<DateTime>(loaded.OrderBy(t => t));

        lock (_sync)
        {
            if (_queues.TryGetValue(key, out var existing))
            {
                return existing;
            }
            _queues[key] = newQueue;
            return newQueue;
        }
    }

    private void TrimQueue(Queue<DateTime> queue, DateTime now)
    {
        while (queue.Count > 0 && now - queue.Peek() > _window)
        {
            queue.Dequeue();
        }
    }

    private void PersistDirtyQueues()
    {
        Dictionary<Guid, DateTime[]> snapshots;
        lock (_sync)
        {
            if (_dirtyTenants.Count == 0)
            {
                return;
            }

            snapshots = _dirtyTenants.ToDictionary(id => id, id => _queues[id].ToArray());
            _dirtyTenants.Clear();
        }

        foreach (var kvp in snapshots)
        {
            _store.Save(kvp.Key, kvp.Value);
        }
    }
}

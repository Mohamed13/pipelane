using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using FluentAssertions;

using Microsoft.Extensions.Options;

using Pipelane.Infrastructure.Background;

using Xunit;

namespace Pipelane.Tests;

public class MessageSendRateLimiterTests
{
    [Fact]
    public async Task TryAcquireAsync_PersistsSnapshots()
    {
        var tenantId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var store = new StubRateLimitStore(new Dictionary<Guid, IReadOnlyCollection<DateTime>>
        {
            [Guid.Empty] = Array.Empty<DateTime>(),
            [tenantId] = new[] { now.AddSeconds(-10) }
        });

        var limiter = new MessageSendRateLimiter(
            Options.Create(new MessagingLimitsOptions
            {
                PerMinuteGlobal = 5,
                PerMinutePerTenant = 2
            }),
            store,
            new FakeTimeProvider(now));

        var first = await limiter.TryAcquireAsync(tenantId);
        var second = await limiter.TryAcquireAsync(tenantId);
        var third = await limiter.TryAcquireAsync(tenantId);

        first.Should().BeTrue();
        second.Should().BeFalse();
        third.Should().BeFalse();

        store.SavedSnapshots.Should().ContainKey(tenantId);
        store.SavedSnapshots[tenantId].Count.Should().Be(2);
    }

    private sealed class StubRateLimitStore : IRateLimitSnapshotStore
    {
        private readonly Dictionary<Guid, IReadOnlyCollection<DateTime>> _initial;
        public Dictionary<Guid, IReadOnlyCollection<DateTime>> SavedSnapshots { get; } = new();

        public StubRateLimitStore(Dictionary<Guid, IReadOnlyCollection<DateTime>> initial)
        {
            _initial = initial;
        }

        public IReadOnlyCollection<DateTime> Load(Guid targetTenantId)
            => _initial.TryGetValue(targetTenantId, out var data) ? data : Array.Empty<DateTime>();

        public void Save(Guid targetTenantId, IReadOnlyCollection<DateTime> hits)
            => SavedSnapshots[targetTenantId] = hits;
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTime _utcNow;

        public FakeTimeProvider(DateTime utcNow) => _utcNow = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}


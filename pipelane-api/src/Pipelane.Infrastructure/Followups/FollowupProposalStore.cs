using System;

using Microsoft.Extensions.Caching.Memory;

using Pipelane.Application.Services;
using Pipelane.Domain.Enums;

namespace Pipelane.Infrastructure.Followups;

public sealed class FollowupProposalStore : IFollowupProposalStore
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(45);
    private readonly IMemoryCache _cache;

    public FollowupProposalStore(IMemoryCache cache)
    {
        _cache = cache;
    }

    /// <inheritdoc/>
    public Guid Save(Guid tenantId, FollowupProposalData proposal)
    {
        var id = Guid.NewGuid();
        var key = BuildKey(tenantId, id);
        _cache.Set(key, proposal, DefaultTtl);
        return id;
    }

    /// <inheritdoc/>
    public bool TryGet(Guid tenantId, Guid proposalId, out FollowupProposalData? proposal)
    {
        if (_cache.TryGetValue(BuildKey(tenantId, proposalId), out var value) && value is FollowupProposalData data)
        {
            proposal = data;
            return true;
        }

        proposal = null;
        return false;
    }

    /// <inheritdoc/>
    public void Remove(Guid tenantId, Guid proposalId)
    {
        _cache.Remove(BuildKey(tenantId, proposalId));
    }

    private static string BuildKey(Guid tenantId, Guid proposalId)
        => $"followup:{tenantId}:{proposalId}";
}

using System;
using Pipelane.Application.Ai;
using Pipelane.Domain.Enums;

namespace Pipelane.Application.Services;

public sealed record FollowupProposalData(
    Channel Channel,
    DateTime ScheduledAtUtc,
    AiFollowupAngle Angle,
    string PreviewText,
    string? Language);

public interface IFollowupProposalStore
{
    Guid Save(Guid tenantId, FollowupProposalData proposal);
    bool TryGet(Guid tenantId, Guid proposalId, out FollowupProposalData? proposal);
    void Remove(Guid tenantId, Guid proposalId);
}

using Pipelane.Domain.Enums;

namespace Pipelane.Application.Services;

public interface IDemoExperienceService
{
    Task<DemoRunResult> RunAsync(Guid tenantId, CancellationToken ct);
}

public sealed record DemoRunResult(DateTime TriggeredAtUtc, IReadOnlyList<DemoRunMessage> Messages);

public sealed record DemoRunMessage(
    Guid ContactId,
    Guid ConversationId,
    Guid MessageId,
    Channel Channel,
    string ContactName,
    MessageStatus Status,
    DateTime CreatedAtUtc);

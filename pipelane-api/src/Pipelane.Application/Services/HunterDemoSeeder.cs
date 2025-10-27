using Pipelane.Application.Hunter;

namespace Pipelane.Application.Services;

public interface IHunterDemoSeeder
{
    Task<IReadOnlyList<HunterResultDto>> SeedAsync(Guid tenantId, CancellationToken ct);
}

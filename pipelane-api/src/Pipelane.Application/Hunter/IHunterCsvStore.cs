using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Pipelane.Application.Hunter;

public interface IHunterCsvStore
{
    Task<Guid> SaveAsync(Guid tenantId, Stream content, CancellationToken ct);
    Task<Stream> OpenAsync(Guid tenantId, Guid csvId, CancellationToken ct);
    Task DeleteAsync(Guid tenantId, Guid csvId, CancellationToken ct);
}

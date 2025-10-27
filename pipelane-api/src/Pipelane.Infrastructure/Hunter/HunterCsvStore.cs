using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Pipelane.Application.Hunter;

namespace Pipelane.Infrastructure.Hunter;

public sealed class HunterCsvStore : IHunterCsvStore
{
    private readonly string _root;
    private readonly ILogger<HunterCsvStore> _logger;

    public HunterCsvStore(IHostEnvironment environment, ILogger<HunterCsvStore> logger)
    {
        _root = Path.Combine(environment.ContentRootPath, "storage", "hunter");
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<Guid> SaveAsync(Guid tenantId, Stream content, CancellationToken ct)
    {
        Directory.CreateDirectory(_root);
        var csvId = Guid.NewGuid();
        var path = BuildPath(tenantId, csvId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using var file = File.Create(path);
        await content.CopyToAsync(file, ct);
        _logger.LogInformation("Saved hunter csv {CsvId} at {Path}", csvId, path);
        return csvId;
    }

    /// <inheritdoc/>
    public Task<Stream> OpenAsync(Guid tenantId, Guid csvId, CancellationToken ct)
    {
        var path = BuildPath(tenantId, csvId);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("CSV introuvable", path);
        }

        Stream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    /// <inheritdoc/>
    public Task DeleteAsync(Guid tenantId, Guid csvId, CancellationToken ct)
    {
        var path = BuildPath(tenantId, csvId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string BuildPath(Guid tenantId, Guid csvId)
        => Path.Combine(_root, tenantId.ToString(), $"{csvId}.csv");
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using FluentAssertions;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

using Pipelane.Api.Controllers;
using Pipelane.Application.Hunter;
using Pipelane.Infrastructure.Persistence;

using Xunit;

namespace Pipelane.Tests;

public class ListsControllerTests
{
    [Fact]
    public async Task Lists_returns_empty_array_when_none()
    {
        var service = new StubHunterService();
        var tenantId = Guid.NewGuid();
        var controller = new ListsController(
            service,
            new StubTenantProvider(tenantId),
            new StubDbDiagnostics(),
            NullLogger<ListsController>.Instance);

        var response = await controller.GetAll(CancellationToken.None);

        var ok = response.Result.Should().BeOfType<OkObjectResult>().Which;
        var payload = ok.Value.Should().BeAssignableTo<IReadOnlyList<ProspectListSummary>>().Which;
        payload.Should().BeEmpty();
    }

    [Fact]
    public async Task Lists_returns_400_when_missing_tenant()
    {
        var service = new StubHunterService();
        var controller = new ListsController(
            service,
            new StubTenantProvider(Guid.Empty),
            new StubDbDiagnostics(),
            NullLogger<ListsController>.Instance);

        var response = await controller.GetAll(CancellationToken.None);

        var badRequest = response.Result.Should().BeOfType<BadRequestObjectResult>().Which;
        var problem = badRequest.Value.Should().BeOfType<ProblemDetails>().Which;
        problem.Title.Should().Be("tenant_header_missing");
        problem.Status.Should().Be(400);
    }

    private sealed class StubHunterService : IHunterService
    {
        public IReadOnlyList<ProspectListSummary> ListsToReturn { get; set; } = Array.Empty<ProspectListSummary>();

        public Task<IReadOnlyList<ProspectListSummary>> GetListsAsync(Guid tenantId, CancellationToken ct)
            => Task.FromResult(ListsToReturn);

        public Task<Guid> UploadCsvAsync(Guid tenantId, StreamReference file, CancellationToken ct) => throw new NotImplementedException();
        public Task<HunterSearchResponse> SearchAsync(Guid tenantId, HunterSearchCriteria criteria, bool dryRun, CancellationToken ct) => throw new NotImplementedException();
        public Task<Guid> CreateListAsync(Guid tenantId, CreateListRequest request, CancellationToken ct) => throw new NotImplementedException();
        public Task<AddToListResponse> AddToListAsync(Guid tenantId, Guid listId, AddToListRequest request, CancellationToken ct) => throw new NotImplementedException();
        public Task<ProspectListResponse> GetListAsync(Guid tenantId, Guid listId, CancellationToken ct) => throw new NotImplementedException();
        public Task RenameListAsync(Guid tenantId, Guid listId, RenameListRequest request, CancellationToken ct) => throw new NotImplementedException();
        public Task DeleteListAsync(Guid tenantId, Guid listId, CancellationToken ct) => throw new NotImplementedException();
        public Task<Guid> CreateCadenceFromListAsync(Guid tenantId, CadenceFromListRequest request, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class StubTenantProvider(Guid tenantId) : ITenantProvider
    {
        public Guid TenantId { get; } = tenantId;
    }

    private sealed class StubDbDiagnostics : IDatabaseDiagnostics
    {
        public string ProviderName => "inmemory";

        public Task<IReadOnlyList<string>> GetPendingMigrationsAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }
}

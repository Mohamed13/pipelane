using System;
using System.Threading;
using System.Threading.Tasks;

using FluentAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Pipelane.Api.Controllers;
using Pipelane.Application.Abstractions;
using Pipelane.Application.DTOs;
using Pipelane.Application.Services;
using Pipelane.Domain.Enums;
using Pipelane.Infrastructure.Background;
using Pipelane.Infrastructure.Persistence;

using Xunit;

namespace Pipelane.Tests;

public class MessagesControllerTests
{
    [Fact]
    public async Task Send_WhenRateLimited_Returns429()
    {
        var tenantId = Guid.NewGuid();
        var controller = new MessagesController(
            new StubMessagingService(),
            new StubTenantProvider(tenantId),
            new StubLimiter(false));

        var result = await controller.Send(CreateRequest(), CancellationToken.None);

        var problem = result as ObjectResult;
        problem.Should().NotBeNull();
        problem!.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
    }

    [Fact]
    public async Task Send_WhenAllowed_ForwardsRequest()
    {
        var svc = new StubMessagingService
        {
            Result = new SendResult(true, "provider-123", null)
        };
        var controller = new MessagesController(
            svc,
            new StubTenantProvider(Guid.NewGuid()),
            new StubLimiter(true));

        var result = await controller.Send(CreateRequest(), CancellationToken.None);

        var ok = result as OkObjectResult;
        ok.Should().NotBeNull();
        ok!.Value.Should().BeEquivalentTo(new SendResult(true, "provider-123", null));
        svc.LastRequest.Should().NotBeNull();
        svc.LastRequest!.Text.Should().Be("Hello");
    }

    private static SendMessageRequest CreateRequest() => new(
        ContactId: Guid.NewGuid(),
        Phone: "+15550000000",
        Channel: Channel.Email,
        Type: "text",
        Text: "Hello",
        TemplateName: null,
        Lang: "en",
        Variables: null,
        Meta: null);

    private sealed class StubMessagingService : IMessagingService
    {
        public SendResult Result { get; set; } = new(true, null, null);
        public SendMessageRequest? LastRequest { get; private set; }

        public Task<SendResult> SendAsync(SendMessageRequest req, CancellationToken ct)
        {
            LastRequest = req;
            return Task.FromResult(Result);
        }
    }

    private sealed class StubTenantProvider : ITenantProvider
    {
        public StubTenantProvider(Guid tenantId) => TenantId = tenantId;
        public Guid TenantId { get; }
    }

    private sealed class StubLimiter : IMessageSendRateLimiter
    {
        private readonly bool _allow;
        public StubLimiter(bool allow) => _allow = allow;
        public Task<bool> TryAcquireAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(_allow);
    }
}

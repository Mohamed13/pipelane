using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FluentAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using Pipelane.Api.Controllers;
using Pipelane.Infrastructure.Persistence;
using Pipelane.Application.Ai;
using Pipelane.Application.Storage;
using Pipelane.Domain.Entities.Prospecting;
using Pipelane.Domain.Enums;

using Xunit;

namespace Pipelane.Tests;

public class AiControllerTests
{
    [Fact]
    public async Task GenerateMessage_ReturnsConflict_WhenProspectOptedOut()
    {
        var options = new DbContextOptionsBuilder<FakeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new FakeDbContext(options);
        var prospectId = Guid.NewGuid();
        db.Prospects.Add(new Prospect
        {
            Id = prospectId,
            TenantId = Guid.NewGuid(),
            Email = "optout@example.com",
            OptedOut = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var stubService = new StubTextAiService();
        var controller = new AiController(
            stubService,
            new StubTenantProvider(Guid.NewGuid()),
            db,
            NullLogger<AiController>.Instance);

        var request = new AiController.GenerateMessageRequest
        {
            ContactId = prospectId,
            Channel = Channel.Email,
            Context = new AiController.GenerateMessageRequestContext { Pitch = "Test" }
        };

        var result = await controller.GenerateMessage(request, CancellationToken.None);

        result.Result.Should().BeOfType<ConflictObjectResult>();
        stubService.GenerateCalled.Should().BeFalse("service should not be invoked when opt-out detected");
    }

    [Fact]
    public async Task ClassifyReply_MapsIntentToResponse()
    {
        var controller = new AiController(
            new StubTextAiService
            {
                ClassifyResponse = new ClassifyReplyResult(AiReplyIntent.Ooo, 0.66, AiContentSource.OpenAi)
            },
            new StubTenantProvider(Guid.NewGuid()),
            new FakeDbContext(new DbContextOptionsBuilder<FakeDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options),
            NullLogger<AiController>.Instance);

        var result = await controller.ClassifyReply(new AiController.ClassifyReplyRequest { Text = "I'm out of office" }, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Which;
        var payload = ok.Value.Should().BeOfType<AiController.ClassifyReplyResponse>().Which;
        payload.Intent.Should().Be("OOO");
        payload.Confidence.Should().BeApproximately(0.66, 0.001);
    }

    [Fact]
    public async Task SuggestFollowup_ReturnsIsoDate()
    {
        var scheduled = DateTime.UtcNow.AddDays(2);
        var stub = new StubTextAiService
        {
            SuggestResponse = new SuggestFollowupResult(scheduled, AiFollowupAngle.Social, "See you soon", AiContentSource.OpenAi)
        };

        var controller = new AiController(
            stub,
            new StubTenantProvider(Guid.NewGuid()),
            new FakeDbContext(new DbContextOptionsBuilder<FakeDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options),
            NullLogger<AiController>.Instance);

        var response = await controller.SuggestFollowup(new AiController.SuggestFollowupRequest
        {
            Channel = Channel.Sms,
            Timezone = "UTC",
            LastInteractionAt = DateTime.UtcNow,
            Read = false
        }, CancellationToken.None);

        var ok = response.Result.Should().BeOfType<OkObjectResult>().Which;
        var payload = ok.Value.Should().BeOfType<AiController.SuggestFollowupResponse>().Which;
        payload.ScheduledAtIso.Should().Be(scheduled.ToString("o"));
        payload.Angle.Should().Be("social");
    }

    private sealed class StubTenantProvider(Guid tenantId) : ITenantProvider
    {
        public Guid TenantId { get; } = tenantId;
    }

    private sealed class StubTextAiService : ITextAiService
    {
        public bool GenerateCalled { get; private set; }

        public ClassifyReplyResult ClassifyResponse { get; init; } = new(AiReplyIntent.Maybe, 0.5, AiContentSource.Fallback);
        public SuggestFollowupResult SuggestResponse { get; init; } = new(DateTime.UtcNow.AddDays(1), AiFollowupAngle.Reminder, "Preview", AiContentSource.Fallback);

        public Task<ClassifyReplyResult> ClassifyReplyAsync(Guid tenantId, ClassifyReplyCommand command, CancellationToken ct) =>
            Task.FromResult(ClassifyResponse);

        public Task<GenerateMessageResult> GenerateMessageAsync(Guid tenantId, GenerateMessageCommand command, CancellationToken ct)
        {
            GenerateCalled = true;
            return Task.FromResult(new GenerateMessageResult("Subject", "Body", "<p>Body</p>", "en", AiContentSource.OpenAi));
        }

        public Task<SuggestFollowupResult> SuggestFollowupAsync(Guid tenantId, SuggestFollowupCommand command, CancellationToken ct) =>
            Task.FromResult(SuggestResponse);
    }
}


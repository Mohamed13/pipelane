using System;
using System.Threading;
using System.Threading.Tasks;

using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Pipelane.Application.Prospecting;
using Pipelane.Domain.Entities.Prospecting;
using Pipelane.Domain.Enums;
using Pipelane.Domain.Enums.Prospecting;

using Xunit;

namespace Pipelane.Tests;

public class ProspectingAiServiceTests
{
    [Fact]
    public async Task GenerateEmail_Fallback_When_No_Api_Key()
    {
        var service = new ProspectingAiService(
            Options.Create(new ProspectingAiOptions { ApiKey = null }),
            NullLogger<ProspectingAiService>.Instance);

        var prospect = new Prospect
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Email = "prospect@example.com",
            FirstName = "Jamie",
            Company = "Acme",
            Status = ProspectStatus.New
        };

        var step = new ProspectingSequenceStep
        {
            Id = Guid.NewGuid(),
            TenantId = prospect.TenantId,
            SequenceId = Guid.NewGuid(),
            Order = 0,
            StepType = SequenceStepType.Email,
            Channel = Channel.Email,
            OffsetDays = 0,
            PromptTemplate = "Test"
        };

        var result = await service.GenerateEmailAsync(prospect, step, null, CancellationToken.None);

        result.subject.Should().NotBeNullOrWhiteSpace();
        result.html.Should().Contain("Unsubscribe", because: "fallback should provide compliance footer");
    }

    [Theory]
    [InlineData("Thanks, I'm interested to learn more next week", ReplyIntent.Interested)]
    [InlineData("Please unsubscribe me", ReplyIntent.Unsubscribe)]
    [InlineData("I'm out of office until Friday", ReplyIntent.OutOfOffice)]
    public async Task ClassifyReply_ReturnsExpectedIntent(string body, ReplyIntent expected)
    {
        var service = new ProspectingAiService(
            Options.Create(new ProspectingAiOptions()),
            NullLogger<ProspectingAiService>.Instance);

        var reply = new ProspectReply
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ProspectId = Guid.NewGuid(),
            ReceivedAtUtc = DateTime.UtcNow,
            TextBody = body
        };

        var (intent, confidence, _) = await service.ClassifyReplyAsync(reply, CancellationToken.None);

        intent.Should().Be(expected);
        confidence.Should().BeGreaterThan(0.4);
    }
}




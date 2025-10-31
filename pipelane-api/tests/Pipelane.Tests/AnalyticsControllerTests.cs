using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using FluentAssertions;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Pipelane.Api.Controllers;
using Pipelane.Application.Analytics;
using Pipelane.Application.Services;
using Pipelane.Application.Storage;
using Pipelane.Domain.Entities;
using Pipelane.Domain.Enums;

using Xunit;

namespace Pipelane.Tests;

public class AnalyticsControllerTests
{
    [Fact]
    public async Task Overview_aggregates_totals_and_channel_counts()
    {
        var options = new DbContextOptionsBuilder<FakeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new FakeDbContext(options);

        var tenant = Guid.NewGuid();
        var conversation = Guid.NewGuid();
        db.Messages.AddRange(
            new Message
            {
                Id = Guid.NewGuid(),
                TenantId = tenant,
                ConversationId = conversation,
                Channel = Channel.Email,
                Direction = MessageDirection.Out,
                Type = MessageType.Text,
                Status = MessageStatus.Delivered,
                CreatedAt = DateTime.UtcNow.AddHours(-2)
            },
            new Message
            {
                Id = Guid.NewGuid(),
                TenantId = tenant,
                ConversationId = conversation,
                Channel = Channel.Sms,
                Direction = MessageDirection.Out,
                Type = MessageType.Text,
                Status = MessageStatus.Failed,
                CreatedAt = DateTime.UtcNow.AddHours(-3)
            },
            new Message
            {
                Id = Guid.NewGuid(),
                TenantId = tenant,
                ConversationId = conversation,
                Channel = Channel.Email,
                Direction = MessageDirection.Out,
                Type = MessageType.Text,
                Status = MessageStatus.Sent,
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            });
        await db.SaveChangesAsync();

        var controller = new AnalyticsController(db, new StubAnalyticsService());

        var actionResult = await controller.Overview(null, null, CancellationToken.None);

        var ok = actionResult.Should().BeOfType<OkObjectResult>().Which;
        var payload = JsonSerializer.SerializeToElement(ok.Value!);

        payload.GetProperty("total").GetInt32().Should().Be(2);

        var channels = payload.GetProperty("byChannel")
            .EnumerateArray()
            .Select(e => (Channel: e.GetProperty("channel").GetString(), Count: e.GetProperty("count").GetInt32()))
            .ToList();

        channels.Should().ContainSingle(x => x.Channel == "email" && x.Count == 1);
        channels.Should().ContainSingle(x => x.Channel == "sms" && x.Count == 1);
    }

    [Fact]
    public async Task Delivery_uses_analytics_service_and_defaults_date_range()
    {
        var stub = new StubAnalyticsService
        {
            DeliveryResult = new DeliveryAnalyticsResult(
                new DeliveryTotals(1, 2, 3, 4, 5, 6),
                Array.Empty<DeliveryChannelBreakdown>(),
                Array.Empty<DeliveryTemplateBreakdown>(),
                Array.Empty<DeliveryTimelinePoint>())
        };

        var options = new DbContextOptionsBuilder<FakeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new FakeDbContext(options);
        var controller = new AnalyticsController(db, stub);
        var beforeCall = DateTime.UtcNow.AddDays(-7).AddMinutes(-1);

        var actionResult = await controller.Delivery(null, null, CancellationToken.None);

        var ok = actionResult.Result.Should().BeOfType<OkObjectResult>().Which;
        ok.Value.Should().BeSameAs(stub.DeliveryResult);

        stub.DeliveryArgs.Should().NotBeNull();
        var (from, to) = stub.DeliveryArgs!.Value;
        from.Should().BeOnOrAfter(beforeCall);
        to.Should().BeOnOrBefore(DateTime.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task TopMessages_uses_analytics_service_for_payload()
    {
        var stub = new StubAnalyticsService
        {
            TopMessagesResult = new TopMessagesResult(
                DateTime.UtcNow.AddDays(-1),
                DateTime.UtcNow,
                new[] { new TopMessageItem("newsletter", "Newsletter", "email", 10, 8, 5, 0, 0, 2) },
                Array.Empty<TopMessageItem>())
        };

        var options = new DbContextOptionsBuilder<FakeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new FakeDbContext(options);
        var controller = new AnalyticsController(db, stub);

        var actionResult = await controller.TopMessages(null, null, CancellationToken.None);

        var ok = actionResult.Result.Should().BeOfType<OkObjectResult>().Which;
        ok.Value.Should().BeSameAs(stub.TopMessagesResult);
        stub.TopMessageArgs.Should().NotBeNull();
    }

    private sealed class StubAnalyticsService : IAnalyticsService
    {
        public DeliveryAnalyticsResult DeliveryResult { get; set; } = new DeliveryAnalyticsResult(
            new DeliveryTotals(0, 0, 0, 0, 0, 0),
            Array.Empty<DeliveryChannelBreakdown>(),
            Array.Empty<DeliveryTemplateBreakdown>(),
            Array.Empty<DeliveryTimelinePoint>());

        public TopMessagesResult TopMessagesResult { get; set; } = new TopMessagesResult(
            DateTime.UtcNow.AddDays(-7),
            DateTime.UtcNow,
            Array.Empty<TopMessageItem>(),
            Array.Empty<TopMessageItem>());

        public (DateTime From, DateTime To)? DeliveryArgs { get; private set; }
        public (DateTime From, DateTime To)? TopMessageArgs { get; private set; }

        public Task<DeliveryAnalyticsResult> GetDeliveryAsync(DateTime from, DateTime to, CancellationToken cancellationToken)
        {
            DeliveryArgs = (from, to);
            return Task.FromResult(DeliveryResult);
        }

        public Task<TopMessagesResult> GetTopMessagesAsync(DateTime from, DateTime to, CancellationToken cancellationToken)
        {
            TopMessageArgs = (from, to);
            return Task.FromResult(TopMessagesResult);
        }
    }
}


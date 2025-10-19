using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Pipelane.Application.Services;
using Pipelane.Domain.Entities;
using Pipelane.Domain.Enums;

using Xunit;

namespace Pipelane.Tests;

public class AnalyticsServiceTests
{
    [Fact]
    public async Task GetDeliveryAsync_ComputesTotalsBreakdowns()
    {
        var options = new DbContextOptionsBuilder<FakeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new FakeDbContext(options);

        var tenantId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        db.Messages.AddRange(
            new Message
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ConversationId = Guid.NewGuid(),
                Channel = Channel.Email,
                Direction = MessageDirection.Out,
                Type = MessageType.Template,
                TemplateName = "welcome",
                Status = MessageStatus.Delivered,
                CreatedAt = now.AddHours(-3)
            },
            new Message
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ConversationId = Guid.NewGuid(),
                Channel = Channel.Email,
                Direction = MessageDirection.Out,
                Type = MessageType.Template,
                TemplateName = "promo",
                Status = MessageStatus.Failed,
                FailedAt = now.AddHours(-2),
                CreatedAt = now.AddHours(-2)
            },
            new Message
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ConversationId = Guid.NewGuid(),
                Channel = Channel.Whatsapp,
                Direction = MessageDirection.Out,
                Type = MessageType.Text,
                Status = MessageStatus.Sent,
                CreatedAt = now.AddHours(-1)
            });

        await db.SaveChangesAsync();

        var service = new AnalyticsService(db);

        var result = await service.GetDeliveryAsync(now.AddHours(-6), now, CancellationToken.None);

        result.Totals.Delivered.Should().Be(1);
        result.Totals.Failed.Should().Be(1);
        result.Totals.Sent.Should().Be(1);
        result.Totals.Bounced.Should().Be(0);

        var email = result.ByChannel.Single(r => r.Channel == "email");
        email.Delivered.Should().Be(1);
        email.Failed.Should().Be(1);
        email.Sent.Should().Be(0);

        var whatsapp = result.ByChannel.Single(r => r.Channel == "whatsapp");
        whatsapp.Sent.Should().Be(1);
        whatsapp.Delivered.Should().Be(0);

        result.ByTemplate.Should().HaveCount(2);
        result.ByTemplate.First(t => t.Template == "promo").Failed.Should().Be(1);
    }

    [Fact]
    public async Task GetDeliveryAsync_NormalisesInvertedRange()
    {
        var options = new DbContextOptionsBuilder<FakeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new FakeDbContext(options);

        var tenantId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        db.Messages.Add(new Message
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ConversationId = Guid.NewGuid(),
            Channel = Channel.Email,
            Direction = MessageDirection.Out,
            Type = MessageType.Template,
            TemplateName = "status",
            Status = MessageStatus.Opened,
            OpenedAt = now.AddHours(-1),
            CreatedAt = now.AddHours(-1)
        });

        await db.SaveChangesAsync();

        var service = new AnalyticsService(db);

        var result = await service.GetDeliveryAsync(now, now.AddHours(-4), CancellationToken.None);

        result.Totals.Opened.Should().Be(1);
        result.ByChannel.Single().Opened.Should().Be(1);
    }
}

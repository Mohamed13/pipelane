using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using FluentAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Pipelane.Api.Controllers;
using Pipelane.Application.Ai;
using Pipelane.Application.Abstractions;
using Pipelane.Application.DTOs;
using Pipelane.Application.Services;
using Pipelane.Domain.Entities;
using Pipelane.Domain.Enums;
using Pipelane.Domain.Enums.Prospecting;
using Pipelane.Infrastructure.Automations;
using Pipelane.Infrastructure.Persistence;

using Xunit;

namespace Pipelane.Tests;

public class SmokeScenarioTests
{
    private static FakeDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<FakeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new FakeDbContext(options);
    }

    [Fact]
    public async Task Smoke_GenerateMessage_And_EnqueueTemplate()
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var contact = new Contact
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = "buyer@acme.co",
            FirstName = "Taylor",
            LastName = "Demo",
            Lang = "en",
            CreatedAt = DateTime.UtcNow.AddDays(-5),
            UpdatedAt = DateTime.UtcNow
        };
        db.Contacts.Add(contact);
        var template = new Template
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "demo_followup",
            Channel = Channel.Email,
            Lang = "en",
            CoreSchemaJson = "{\"vars\":[\"firstName\"]}",
            IsActive = true,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.Templates.Add(template);
        await db.SaveChangesAsync();

        var aiService = new StubTextAiService();
        var controller = new AiController(aiService, new StubTenantProvider(tenantId), db, NullLogger<AiController>.Instance);

        var generateRequest = new AiController.GenerateMessageRequest
        {
            Channel = Channel.Email,
            Context = new AiController.GenerateMessageRequestContext
            {
                FirstName = "Taylor",
                Company = "Acme Co",
                Pitch = "We help revenue teams close multi-channel gaps.",
                CalendlyUrl = "https://calendly.com/demo",
                PainPoints = new List<string> { "manual follow-ups" }
            }
        };

        var preview = await controller.GenerateMessage(generateRequest, CancellationToken.None);
        var ok = preview.Result.Should().BeOfType<OkObjectResult>().Which;
        var body = ok.Value.Should().BeOfType<AiController.GenerateMessageResponse>().Which;
        body.Subject.Should().Be("Quick idea for Acme Co");
        body.Text.Should().Contain("multi-channel");

        var channel = new StubMessageChannel(Channel.Email);
        var registry = new ChannelRegistry(new[] { channel });
        var rules = new ChannelRulesService(db);
        var outbox = new OutboxService(db);
        var messaging = new MessagingService(db, registry, rules, outbox);

        var sendResult = await messaging.SendAsync(
            new SendMessageRequest(contact.Id, null, Channel.Email, "template", null, "demo_followup", "en", new Dictionary<string, string> { ["firstName"] = "Taylor" }, null),
            CancellationToken.None);

        sendResult.Success.Should().BeTrue();
        db.Outbox.Should().ContainSingle();
    }

    [Fact]
    public async Task Smoke_ClassifyReply_ReturnsInterested()
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var aiService = new StubTextAiService
        {
            ClassifyResponse = new ClassifyReplyResult(AiReplyIntent.Interested, 0.91, AiContentSource.OpenAi)
        };
        var controller = new AiController(aiService, new StubTenantProvider(tenantId), db, NullLogger<AiController>.Instance);

        var response = await controller.ClassifyReply(new AiController.ClassifyReplyRequest
        {
            Text = "Yes, let's talk tomorrow morning."
        }, CancellationToken.None);

        var ok = response.Result.Should().BeOfType<OkObjectResult>().Which;
        var payload = ok.Value.Should().BeOfType<AiController.ClassifyReplyResponse>().Which;
        payload.Intent.Should().Be("Interested");
        payload.Confidence.Should().BeGreaterThan(0.8);
    }

    [Fact]
    public async Task Smoke_ScheduleFollowup_PopulatesOutbox()
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var contact = new Contact
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = "prospect@demo.io",
            FirstName = "Morgan",
            LastName = "Seed",
            Lang = "en",
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow
        };
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();

        var options = new StaticOptionsMonitor<AutomationsOptions>(new AutomationsOptions
        {
            ActionsEnabled = true,
            EventsEnabled = false,
            Token = "secret",
            RateLimitPerMinute = 300
        });

        var controller = new AutomationsController(
            options,
            new MessagingService(db, new ChannelRegistry(Array.Empty<IMessageChannel>()), new ChannelRulesService(db), new OutboxService(db)),
            new OutboxService(db),
            db,
            new StubTenantProvider(tenantId),
            NullLogger<AutomationsController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        controller.HttpContext.Request.Headers["X-Automations-Token"] = "secret";
        controller.HttpContext.Request.Headers["X-Tenant-Id"] = tenantId.ToString();

        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        jsonOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

        var payload = JsonSerializer.SerializeToElement(
            new
            {
                contactId = contact.Id,
                channel = Channel.Email,
                scheduledAtUtc = "2025-01-21T10:30:00Z",
                mode = "text",
                text = "Checking if Tuesday 10:30 works for a quick sync.",
            },
            jsonOptions
        );

        var request = new AutomationsController.AutomationActionRequest
        {
            Type = "schedule_followup",
            Data = payload,
        };

        var result = await controller.Handle(request, CancellationToken.None);
        result.Should().BeOfType<OkObjectResult>();
        db.Outbox.Should().ContainSingle();
        var job = await db.Outbox.SingleAsync();
        job.ScheduledAtUtc.Should().Be(DateTime.Parse("2025-01-21T10:30:00Z").ToUniversalTime());
        job.Type.Should().Be(MessageType.Text);
    }

    private sealed class StubTenantProvider(Guid tenantId) : ITenantProvider
    {
        public Guid TenantId { get; } = tenantId;
    }

    private sealed class StubTextAiService : ITextAiService
    {
        public ClassifyReplyResult ClassifyResponse { get; set; } = new(AiReplyIntent.Maybe, 0.5, AiContentSource.Fallback);
        public SuggestFollowupResult SuggestResponse { get; set; } = new(DateTime.UtcNow.AddDays(1), AiFollowupAngle.Value, "Sharing a quick win", AiContentSource.Fallback);

        public Task<ClassifyReplyResult> ClassifyReplyAsync(Guid tenantId, ClassifyReplyCommand command, CancellationToken ct)
            => Task.FromResult(ClassifyResponse);

        public Task<GenerateMessageResult> GenerateMessageAsync(Guid tenantId, GenerateMessageCommand command, CancellationToken ct)
            => Task.FromResult(new GenerateMessageResult("Quick idea for Acme Co", "Hello Taylor,\nWe help revenue teams close multi-channel gaps.", "<p>Hello Taylor,</p>", "en", AiContentSource.Fallback));

        public Task<SuggestFollowupResult> SuggestFollowupAsync(Guid tenantId, SuggestFollowupCommand command, CancellationToken ct)
            => Task.FromResult(SuggestResponse);
    }

    private sealed class StubMessageChannel(Channel channel) : IMessageChannel
    {
        public Channel Channel { get; } = channel;

        public Task<SendResult> SendTemplateAsync(Contact c, Template t, IDictionary<string, string> vars, SendMeta meta, CancellationToken ct)
            => Task.FromResult(new SendResult(true, Guid.NewGuid().ToString(), null));

        public Task<SendResult> SendTextAsync(Contact c, string text, SendMeta meta, CancellationToken ct)
            => Task.FromResult(new SendResult(true, Guid.NewGuid().ToString(), null));

        public Task<WebhookResult> HandleWebhookAsync(string body, IDictionary<string, string> headers, CancellationToken ct)
            => Task.FromResult(new WebhookResult(true, null));

        public Task<bool> ValidateTemplateAsync(Template t, CancellationToken ct)
            => Task.FromResult(true);
    }

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue { get; } = value;

        public T Get(string? name) => CurrentValue;

        public IDisposable OnChange(Action<T, string?> listener) => EmptyDisposable.Instance;

        private sealed class EmptyDisposable : IDisposable
        {
            public static readonly EmptyDisposable Instance = new();
            public void Dispose() { }
        }
    }
}

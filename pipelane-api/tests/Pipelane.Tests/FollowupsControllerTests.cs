using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using FluentAssertions;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Pipelane.Api.Controllers;
using Pipelane.Application.Ai;
using Pipelane.Application.DTOs;
using Pipelane.Application.Services;
using Pipelane.Application.Storage;
using Pipelane.Domain.Entities;
using Pipelane.Domain.Enums;
using Pipelane.Infrastructure.Background;
using Pipelane.Infrastructure.Persistence;

using Xunit;

namespace Pipelane.Tests;

public class FollowupsControllerTests
{
    [Fact]
    public async Task Preview_get_and_post_compat()
    {
        var options = new DbContextOptionsBuilder<FakeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new FakeDbContext(options);

        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();

        db.Contacts.Add(new Contact
        {
            Id = contactId,
            TenantId = tenantId,
            Lang = "fr",
            Email = "contact@example.com",
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            UpdatedAt = DateTime.UtcNow.AddDays(-5)
        });
        db.Conversations.Add(new Conversation
        {
            Id = conversationId,
            TenantId = tenantId,
            ContactId = contactId,
            PrimaryChannel = Channel.Email,
            CreatedAt = DateTime.UtcNow.AddDays(-3)
        });
        db.Messages.Add(new Message
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ConversationId = conversationId,
            Channel = Channel.Email,
            Direction = MessageDirection.Out,
            Type = MessageType.Text,
            PayloadJson = JsonSerializer.Serialize(new { text = "Bonjour" }),
            Status = MessageStatus.Delivered,
            CreatedAt = DateTime.UtcNow.AddHours(-6),
            Lang = "fr"
        });
        db.Messages.Add(new Message
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ConversationId = conversationId,
            Channel = Channel.Email,
            Direction = MessageDirection.In,
            Type = MessageType.Text,
            PayloadJson = JsonSerializer.Serialize(new { text = "Merci" }),
            Status = MessageStatus.Opened,
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            Lang = "fr"
        });
        await db.SaveChangesAsync();

        var store = new StubProposalStore();
        var ai = new StubAiService
        {
            SuggestResponse = new SuggestFollowupResult(DateTime.UtcNow.AddHours(4), AiFollowupAngle.Value, "On se capte ?", AiContentSource.Fallback)
        };

        var controller = new FollowupsController(
            db,
            new StubOutboxService(),
            store,
            new StubTenantProvider(tenantId),
            ai,
            Options.Create(new MessagingLimitsOptions()),
            NullLogger<FollowupsController>.Instance);

        var getResponse = await controller.PreviewConversation(conversationId, CancellationToken.None);
        var getOk = getResponse.Result.Should().BeOfType<OkObjectResult>().Which;
        var getPreview = getOk.Value.Should().BeOfType<FollowupsController.FollowupConversationPreviewResponse>().Which;
        getPreview.Proposal.PreviewText.Should().Be("On se capte ?");

        var postResponse = await controller.Preview(new FollowupPreviewRequest { ConversationId = conversationId }, CancellationToken.None);
        var postOk = postResponse.Result.Should().BeOfType<OkObjectResult>().Which;
        postOk.Value.Should().BeOfType<FollowupsController.FollowupConversationPreviewResponse>();
    }

    [Fact]
    public async Task Preview_400_on_missing_conversationId()
    {
        var options = new DbContextOptionsBuilder<FakeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new FakeDbContext(options);

        var controller = new FollowupsController(
            db,
            new StubOutboxService(),
            new StubProposalStore(),
            new StubTenantProvider(Guid.NewGuid()),
            new StubAiService(),
            Options.Create(new MessagingLimitsOptions()),
            NullLogger<FollowupsController>.Instance);

        var getResponse = await controller.PreviewConversation(Guid.Empty, CancellationToken.None);
        var getBadRequest = getResponse.Result.Should().BeOfType<BadRequestObjectResult>().Which;
        var getProblem = getBadRequest.Value.Should().BeOfType<ProblemDetails>().Which;
        getProblem.Detail.Should().Be("conversationId required");

        var nullResponse = await controller.Preview(null, CancellationToken.None);
        var nullBadRequest = nullResponse.Result.Should().BeOfType<BadRequestObjectResult>().Which;
        var nullProblem = nullBadRequest.Value.Should().BeOfType<ProblemDetails>().Which;
        nullProblem.Detail.Should().Be("conversationId required");

        var postResponse = await controller.Preview(new FollowupPreviewRequest { ConversationId = Guid.Empty }, CancellationToken.None);
        var postBadRequest = postResponse.Result.Should().BeOfType<BadRequestObjectResult>().Which;
        var postProblem = postBadRequest.Value.Should().BeOfType<ProblemDetails>().Which;
        postProblem.Detail.Should().Be("conversationId required");
    }

    [Fact]
    public async Task ValidateFollowup_Enqueues_Outbox_Message_With_Proposal_Data()
    {
        var options = new DbContextOptionsBuilder<FakeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new FakeDbContext(options);

        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var scheduledAt = DateTime.UtcNow.AddHours(3);

        db.Contacts.Add(new Contact
        {
            Id = contactId,
            TenantId = tenantId,
            Email = "demo@example.com",
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        });
        db.Conversations.Add(new Conversation
        {
            Id = conversationId,
            TenantId = tenantId,
            ContactId = contactId,
            PrimaryChannel = Channel.Email,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        });
        await db.SaveChangesAsync();

        var outbox = new StubOutboxService();
        var store = new StubProposalStore();
        store.Seed(proposalId, new FollowupProposalData(
            Channel.Email,
            scheduledAt,
            AiFollowupAngle.Value,
            "Hello again!",
            "fr"));
        var ai = new StubAiService();

        var controller = new FollowupsController(
            db,
            outbox,
            store,
            new StubTenantProvider(tenantId),
            ai,
            Options.Create(new MessagingLimitsOptions()),
            NullLogger<FollowupsController>.Instance);

        var response = await controller.Validate(
            new FollowupsController.ValidateFollowupRequest(conversationId, proposalId, SendNow: false),
            CancellationToken.None);

        var ok = response.Result.Should().BeOfType<OkObjectResult>().Which;
        var payloadJson = JsonSerializer.Serialize(ok.Value);
        using var doc = JsonDocument.Parse(payloadJson);
        doc.RootElement.GetProperty("conversationId").GetGuid().Should().Be(conversationId);
        doc.RootElement.GetProperty("scheduledAt").GetDateTime().Should().Be(scheduledAt);

        outbox.LastMessage.Should().NotBeNull();
        outbox.LastMessage!.ContactId.Should().Be(contactId);
        outbox.LastMessage!.ConversationId.Should().Be(conversationId);
        outbox.LastMessage!.Channel.Should().Be(Channel.Email);
        outbox.LastMessage!.ScheduledAtUtc.Should().Be(scheduledAt);
        var payload = JsonDocument.Parse(outbox.LastMessage.PayloadJson!);
        payload.RootElement.GetProperty("text").GetString().Should().Be("Hello again!");
        var meta = JsonDocument.Parse(outbox.LastMessage.MetaJson!);
        meta.RootElement.GetProperty("angle").GetString().Should().Be("value");
        meta.RootElement.GetProperty("language").GetString().Should().Be("fr");
        store.Removed.Should().BeTrue("proposal should be removed after enqueue");
    }

    [Fact]
    public async Task ValidateFollowup_SendNow_QueuesImmediateMessage()
    {
        var options = new DbContextOptionsBuilder<FakeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new FakeDbContext(options);

        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();

        db.Contacts.Add(new Contact
        {
            Id = contactId,
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            UpdatedAt = DateTime.UtcNow.AddDays(-2)
        });
        db.Conversations.Add(new Conversation
        {
            Id = conversationId,
            TenantId = tenantId,
            ContactId = contactId,
            PrimaryChannel = Channel.Whatsapp,
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        });
        await db.SaveChangesAsync();

        var outbox = new StubOutboxService();
        var store = new StubProposalStore();
        store.Seed(proposalId, new FollowupProposalData(
            Channel.Whatsapp,
            DateTime.UtcNow.AddHours(1),
            AiFollowupAngle.Reminder,
            "Ready for a quick chat?",
            "en"));
        var ai = new StubAiService();

        var controller = new FollowupsController(
            db,
            outbox,
            store,
            new StubTenantProvider(tenantId),
            ai,
            Options.Create(new MessagingLimitsOptions()),
            NullLogger<FollowupsController>.Instance);

        var before = DateTime.UtcNow;
        var response = await controller.Validate(
            new FollowupsController.ValidateFollowupRequest(conversationId, proposalId, SendNow: true),
            CancellationToken.None);
        var after = DateTime.UtcNow;

        var ok = response.Result.Should().BeOfType<OkObjectResult>().Which;
        var payloadJson = JsonSerializer.Serialize(ok.Value);
        using var doc = JsonDocument.Parse(payloadJson);
        var scheduledAt = doc.RootElement.GetProperty("scheduledAt").GetDateTime();
        scheduledAt.Should().BeOnOrAfter(before);
        scheduledAt.Should().BeOnOrBefore(after.AddSeconds(1));

        outbox.LastMessage.Should().NotBeNull();
        outbox.LastMessage!.ScheduledAtUtc.Should().BeNull("send-now messages should enqueue immediately");
        store.Removed.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateFollowup_ReturnsNotFound_WhenProposalMissing()
    {
        var options = new DbContextOptionsBuilder<FakeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new FakeDbContext(options);

        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();

        db.Contacts.Add(new Contact
        {
            Id = contactId,
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow.AddDays(-5),
            UpdatedAt = DateTime.UtcNow.AddDays(-5)
        });
        db.Conversations.Add(new Conversation
        {
            Id = conversationId,
            TenantId = tenantId,
            ContactId = contactId,
            PrimaryChannel = Channel.Sms,
            CreatedAt = DateTime.UtcNow.AddDays(-5)
        });
        await db.SaveChangesAsync();

        var store = new StubProposalStore { Enabled = false };
        var controller = new FollowupsController(
            db,
            new StubOutboxService(),
            store,
            new StubTenantProvider(tenantId),
            new StubAiService(),
            Options.Create(new MessagingLimitsOptions()),
            NullLogger<FollowupsController>.Instance);

        var response = await controller.Validate(
            new FollowupsController.ValidateFollowupRequest(conversationId, proposalId, SendNow: false),
            CancellationToken.None);

        response.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task PreviewConversation_ReturnsSuggestionAndStoresProposal()
    {
        var options = new DbContextOptionsBuilder<FakeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new FakeDbContext(options);

        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();

        db.Contacts.Add(new Contact
        {
            Id = contactId,
            TenantId = tenantId,
            FirstName = "Alicia",
            Lang = "fr",
            CreatedAt = DateTime.UtcNow.AddDays(-3),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        });
        db.Conversations.Add(new Conversation
        {
            Id = conversationId,
            TenantId = tenantId,
            ContactId = contactId,
            PrimaryChannel = Channel.Email,
            CreatedAt = DateTime.UtcNow.AddDays(-3)
        });
        db.Messages.Add(new Message
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ConversationId = conversationId,
            Channel = Channel.Email,
            Direction = MessageDirection.In,
            Type = MessageType.Text,
            PayloadJson = JsonSerializer.Serialize(new { text = "Merci pour l'info !" }),
            Status = MessageStatus.Opened,
            CreatedAt = DateTime.UtcNow.AddHours(-2)
        });
        db.Messages.Add(new Message
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ConversationId = conversationId,
            Channel = Channel.Email,
            Direction = MessageDirection.Out,
            Type = MessageType.Text,
            PayloadJson = JsonSerializer.Serialize(new { text = "Ravi d'avoir vos nouvelles." }),
            Status = MessageStatus.Delivered,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            Lang = "fr"
        });
        await db.SaveChangesAsync();

        var store = new StubProposalStore();
        var ai = new StubAiService
        {
            SuggestResponse = new SuggestFollowupResult(
                DateTime.UtcNow.AddHours(4),
                AiFollowupAngle.Social,
                "On se retrouve demain ?",
                AiContentSource.Fallback)
        };

        var controller = new FollowupsController(
            db,
            new StubOutboxService(),
            store,
            new StubTenantProvider(tenantId),
            ai,
            Options.Create(new MessagingLimitsOptions()),
            NullLogger<FollowupsController>.Instance);

        var response = await controller.PreviewConversation(conversationId, CancellationToken.None);

        var ok = response.Result.Should().BeOfType<OkObjectResult>().Which;
        var preview = ok.Value.Should().BeOfType<FollowupsController.FollowupConversationPreviewResponse>().Which;
        preview.HistorySnippet.Should().Contain("Client:");
        preview.Proposal.PreviewText.Should().Be("On se retrouve demain ?");
        store.LastSavedProposal.Should().NotBeNull();
        store.LastSavedProposal!.Angle.Should().Be(AiFollowupAngle.Social);
        preview.Proposal.ProposalId.Should().NotBe(Guid.Empty);
    }

    private sealed class StubOutboxService : IOutboxService
    {
        public OutboxMessage? LastMessage { get; private set; }

        public Task EnqueueAsync(OutboxMessage message, CancellationToken ct)
        {
            LastMessage = message;
            return Task.CompletedTask;
        }
    }

    private sealed class StubProposalStore : IFollowupProposalStore
    {
        private readonly Dictionary<Guid, FollowupProposalData> _proposals = new();

        public bool Removed { get; private set; }
        public bool Enabled { get; set; } = true;
        public FollowupProposalData? LastSavedProposal { get; private set; }
        public Guid? LastSavedTenantId { get; private set; }

        public Guid Save(Guid tenantId, FollowupProposalData proposal)
        {
            var id = Guid.NewGuid();
            _proposals[id] = proposal;
            LastSavedProposal = proposal;
            LastSavedTenantId = tenantId;
            return id;
        }

        public bool TryGet(Guid tenantId, Guid proposalId, out FollowupProposalData? proposal)
        {
            if (!Enabled)
            {
                proposal = null;
                return false;
            }

            return _proposals.TryGetValue(proposalId, out proposal);
        }

        public void Remove(Guid tenantId, Guid proposalId)
        {
            Removed = true;
            _proposals.Remove(proposalId);
        }

        public void Seed(Guid proposalId, FollowupProposalData proposal)
        {
            _proposals[proposalId] = proposal;
        }
    }

    private sealed class StubTenantProvider(Guid tenantId) : ITenantProvider
    {
        public Guid TenantId { get; } = tenantId;
    }

    private sealed class StubAiService : ITextAiService
    {
        public SuggestFollowupResult SuggestResponse { get; set; } = new(DateTime.UtcNow.AddHours(1), AiFollowupAngle.Reminder, "Preview", AiContentSource.Fallback);

        public Task<ClassifyReplyResult> ClassifyReplyAsync(Guid tenantId, ClassifyReplyCommand command, CancellationToken ct) =>
            Task.FromResult(new ClassifyReplyResult(AiReplyIntent.Maybe, 0.5, AiContentSource.Fallback));

        public Task<GenerateMessageResult> GenerateMessageAsync(Guid tenantId, GenerateMessageCommand command, CancellationToken ct) =>
            Task.FromResult(new GenerateMessageResult(null, "Hello", "<p>Hello</p>", "en", AiContentSource.Fallback));

        public Task<SuggestFollowupResult> SuggestFollowupAsync(Guid tenantId, SuggestFollowupCommand command, CancellationToken ct) =>
            Task.FromResult(SuggestResponse);
    }
}

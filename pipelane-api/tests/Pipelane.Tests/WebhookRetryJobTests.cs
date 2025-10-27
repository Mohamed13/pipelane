using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using Pipelane.Application.Abstractions;
using Pipelane.Domain.Entities;
using Pipelane.Domain.Enums;
using Pipelane.Infrastructure.Webhooks;

using Xunit;

namespace Pipelane.Tests;

public class WebhookRetryJobTests
{
    [Fact]
    public async Task Execute_ReplaysSuccessfulWebhook()
    {
        var item = new WebhookDeadLetterItem(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Channel.Sms,
            "twilio",
            "status",
            "MessageSid=SM1",
            new Dictionary<string, string>(),
            RetryCount: 0);

        var store = new StubDeadLetterStore(item);
        var channel = new StubChannel(Channel.Sms, new WebhookResult(true, null));
        var job = new WebhookRetryJob(store, new[] { channel }, new NullLogger<WebhookRetryJob>());

        await job.Execute(null!);

        store.SuccessIds.Should().Contain(item.Id);
        store.FailureRecords.Should().BeEmpty();
        channel.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task Execute_RequeuesOnFailure()
    {
        var item = new WebhookDeadLetterItem(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Channel.Whatsapp,
            "whatsapp",
            "status",
            "{}",
            new Dictionary<string, string>(),
            RetryCount: 1);

        var store = new StubDeadLetterStore(item);
        var channel = new StubChannel(Channel.Whatsapp, new WebhookResult(false, "invalid_signature"));
        var job = new WebhookRetryJob(store, new[] { channel }, new NullLogger<WebhookRetryJob>());

        await job.Execute(null!);

        store.FailureRecords.Should().ContainKey(item.Id);
        store.SuccessIds.Should().BeEmpty();
        channel.CallCount.Should().Be(1);
    }

    private sealed class StubDeadLetterStore : IWebhookDeadLetterStore
    {
        private readonly Queue<WebhookDeadLetterItem> _items;

        public StubDeadLetterStore(params WebhookDeadLetterItem[] items)
        {
            _items = new Queue<WebhookDeadLetterItem>(items);
        }

        public List<Guid> SuccessIds { get; } = new();
        public Dictionary<Guid, string> FailureRecords { get; } = new();

        public Task LogFailureAsync(Guid tenantId, Channel channel, string provider, string kind, string payload, IDictionary<string, string> headers, string error, CancellationToken ct)
            => Task.CompletedTask;

        public Task<IReadOnlyList<WebhookDeadLetterItem>> TakeDueAsync(DateTime utcNow, int batchSize, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<WebhookDeadLetterItem>>(_items.ToList());

        public Task MarkSuccessAsync(Guid id, CancellationToken ct)
        {
            SuccessIds.Add(id);
            return Task.CompletedTask;
        }

        public Task MarkFailureAsync(Guid id, string error, TimeSpan backoff, CancellationToken ct)
        {
            FailureRecords[id] = error;
            return Task.CompletedTask;
        }
    }

    private sealed class StubChannel : IMessageChannel
    {
        private readonly WebhookResult _result;

        public StubChannel(Channel channel, WebhookResult result)
        {
            Channel = channel;
            _result = result;
        }

        public Channel Channel { get; }
        public int CallCount { get; private set; }

        public Task<SendResult> SendTemplateAsync(Contact contact, Template template, IDictionary<string, string> variables, SendMeta meta, CancellationToken ct)
            => Task.FromResult(new SendResult(false, null, "not_supported"));

        public Task<SendResult> SendTextAsync(Contact contact, string text, SendMeta meta, CancellationToken ct)
            => Task.FromResult(new SendResult(false, null, "not_supported"));

        public Task<bool> ValidateTemplateAsync(Template template, CancellationToken ct) => Task.FromResult(true);

        public Task<WebhookResult> HandleWebhookAsync(string body, IDictionary<string, string> headers, CancellationToken ct)
        {
            CallCount++;
            return Task.FromResult(_result);
        }
    }
}


using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using Pipelane.Infrastructure.Channels;

using Xunit;

namespace Pipelane.Tests;

public class ChannelWebhookTests
{
    [Fact]
    public async Task WhatsAppWebhook_ShouldValidateSignature()
    {
        var options = new DbContextOptionsBuilder<FakeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new FakeDbContext(options);

        var tenantId = Guid.NewGuid();
        var secret = "super-secret";
        var provider = new StubChannelConfigurationProvider();
        provider.SetWhatsApp(tenantId, new WhatsAppChannelConfig("app", secret, "waba", "phone", "token", "verify"));

        var channel = new WhatsAppChannel(new FakeHttpClientFactory(), provider, db, NullLogger<WhatsAppChannel>.Instance);
        var payload = "{\"entry\":[]}";
        var signature = $"sha256={ComputeWhatsAppSignature(payload, secret)}";
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["x-tenant-id"] = tenantId.ToString(),
            ["X-Hub-Signature-256"] = signature
        };

        var result = await channel.HandleWebhookAsync(payload, headers, CancellationToken.None);

        result.Ok.Should().BeTrue();
    }

    [Fact]
    public async Task WhatsAppWebhook_ShouldUpdateStatus()
    {
        var options = new DbContextOptionsBuilder<FakeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new FakeDbContext(options);

        var tenantId = Guid.NewGuid();
        var secret = "another-secret";
        var provider = new StubChannelConfigurationProvider();
        provider.SetWhatsApp(tenantId, new WhatsAppChannelConfig("app", secret, "waba", "phone", "token", "verify"));

        var contactId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var messageId = "wamid-123";
        db.Contacts.Add(new Domain.Entities.Contact { Id = contactId, TenantId = tenantId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        db.Conversations.Add(new Domain.Entities.Conversation { Id = conversationId, TenantId = tenantId, ContactId = contactId, PrimaryChannel = Domain.Enums.Channel.Whatsapp, CreatedAt = DateTime.UtcNow });
        db.Messages.Add(new Domain.Entities.Message
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ConversationId = conversationId,
            Channel = Domain.Enums.Channel.Whatsapp,
            Direction = Domain.Enums.MessageDirection.Out,
            Type = Domain.Enums.MessageType.Text,
            Provider = "whatsapp",
            ProviderMessageId = messageId,
            Status = Domain.Enums.MessageStatus.Sent,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var channel = new WhatsAppChannel(new FakeHttpClientFactory(), provider, db, NullLogger<WhatsAppChannel>.Instance);
        var payloadObject = new
        {
            entry = new[]
            {
                new
                {
                    changes = new[]
                    {
                        new
                        {
                            value = new
                            {
                                statuses = new[]
                                {
                                    new { id = messageId, status = "delivered", timestamp = "123" }
                                }
                            }
                        }
                    }
                }
            }
        };
        var payload = JsonSerializer.Serialize(payloadObject);
        var signature = $"sha256={ComputeWhatsAppSignature(payload, secret)}";
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["x-tenant-id"] = tenantId.ToString(),
            ["X-Hub-Signature-256"] = signature
        };

        var result = await channel.HandleWebhookAsync(payload, headers, CancellationToken.None);
        result.Ok.Should().BeTrue();

        var message = await db.Messages.SingleAsync(m => m.ProviderMessageId == messageId);
        message.Status.Should().Be(Domain.Enums.MessageStatus.Delivered);
        (await db.MessageEvents.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task TwilioWebhook_ShouldRejectInvalidSignature()
    {
        var options = new DbContextOptionsBuilder<FakeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new FakeDbContext(options);

        var tenantId = Guid.NewGuid();
        var provider = new StubChannelConfigurationProvider();
        provider.SetTwilio(tenantId, new TwilioSmsChannelConfig("sid", "auth", null, "+123"));

        var channel = new SmsChannel(new FakeHttpClientFactory(), provider, db, NullLogger<SmsChannel>.Instance);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["x-tenant-id"] = tenantId.ToString(),
            ["x-webhook-kind"] = "status",
            ["X-Twilio-Signature"] = "invalid",
            ["x-request-url"] = "https://example.org/api/webhooks/sms/twilio/status"
        };

        var result = await channel.HandleWebhookAsync("MessageSid=SM123", headers, CancellationToken.None);

        result.Ok.Should().BeFalse();
        result.Reason.Should().Be("invalid_signature");
    }

    [Fact]
    public async Task TwilioWebhook_ShouldUpdateStatus()
    {
        var options = new DbContextOptionsBuilder<FakeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new FakeDbContext(options);

        var tenantId = Guid.NewGuid();
        var authToken = "twilio-secret";
        var provider = new StubChannelConfigurationProvider();
        provider.SetTwilio(tenantId, new TwilioSmsChannelConfig("sid", authToken, null, "+123"));

        var conversationId = Guid.NewGuid();
        var contact = new Domain.Entities.Contact { Id = Guid.NewGuid(), TenantId = tenantId, Phone = "+15550009988", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.Contacts.Add(contact);
        db.Conversations.Add(new Domain.Entities.Conversation { Id = conversationId, TenantId = tenantId, ContactId = contact.Id, PrimaryChannel = Domain.Enums.Channel.Sms, CreatedAt = DateTime.UtcNow });
        db.Messages.Add(new Domain.Entities.Message
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ConversationId = conversationId,
            Channel = Domain.Enums.Channel.Sms,
            Direction = Domain.Enums.MessageDirection.Out,
            Type = Domain.Enums.MessageType.Text,
            Provider = "twilio",
            ProviderMessageId = "SM999",
            Status = Domain.Enums.MessageStatus.Sent,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var channel = new SmsChannel(new FakeHttpClientFactory(), provider, db, NullLogger<SmsChannel>.Instance);
        var body = "MessageSid=SM999&MessageStatus=delivered";
        var requestUrl = "https://example.org/api/webhooks/sms/twilio/status";
        var signature = ComputeTwilioSignature(requestUrl, body, authToken);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["x-tenant-id"] = tenantId.ToString(),
            ["x-webhook-kind"] = "status",
            ["x-request-url"] = requestUrl,
            ["X-Twilio-Signature"] = signature
        };

        var result = await channel.HandleWebhookAsync(body, headers, CancellationToken.None);
        result.Ok.Should().BeTrue();

        var message = await db.Messages.SingleAsync(m => m.ProviderMessageId == "SM999");
        message.Status.Should().Be(Domain.Enums.MessageStatus.Delivered);
        (await db.MessageEvents.CountAsync()).Should().Be(1);
    }

    private static string ComputeWhatsAppSignature(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string ComputeTwilioSignature(string url, string body, string authToken)
    {
        var builder = new StringBuilder(url);
        var pairs = body.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part =>
            {
                var segments = part.Split('=', 2);
                var key = segments[0];
                var value = segments.Length > 1 ? Uri.UnescapeDataString(segments[1]) : string.Empty;
                return new KeyValuePair<string, string>(key, value);
            })
            .OrderBy(kvp => kvp.Key, StringComparer.Ordinal);

        foreach (var kvp in pairs)
        {
            builder.Append(kvp.Key).Append(kvp.Value);
        }

        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(authToken));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToBase64String(hash);
    }

    private sealed class StubChannelConfigurationProvider : IChannelConfigurationProvider
    {
        private readonly Dictionary<Guid, WhatsAppChannelConfig> _whatsapp = new();
        private readonly Dictionary<Guid, TwilioSmsChannelConfig> _twilio = new();

        public void SetWhatsApp(Guid tenantId, WhatsAppChannelConfig config) => _whatsapp[tenantId] = config;
        public void SetTwilio(Guid tenantId, TwilioSmsChannelConfig config) => _twilio[tenantId] = config;

        public Task<WhatsAppChannelConfig?> GetWhatsAppConfigAsync(Guid tenantId, CancellationToken ct)
            => Task.FromResult(_whatsapp.TryGetValue(tenantId, out var config) ? config : null);

        public Task<TwilioSmsChannelConfig?> GetTwilioConfigAsync(Guid tenantId, CancellationToken ct)
            => Task.FromResult(_twilio.TryGetValue(tenantId, out var config) ? config : null);
    }

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}

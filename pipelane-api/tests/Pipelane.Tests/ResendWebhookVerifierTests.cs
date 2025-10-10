using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

using FluentAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

using Pipelane.Infrastructure.Webhooks;

using Xunit;

namespace Pipelane.Tests;

public class ResendWebhookVerifierTests
{
    [Fact]
    public void Verify_WithValidSignature_ReturnsTrue()
    {
        const string secret = "test-secret";
        const string payload = "{\"example\":1}";
        var expectedSignature = ComputeSignature(secret, payload);

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RESEND_WEBHOOK_SECRET"] = secret
        }).Build();

        var verifier = new ResendWebhookVerifier(config, NullLogger<ResendWebhookVerifier>.Instance);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["resend-signature"] = expectedSignature
        };

        verifier.Verify("resend", payload, headers).Should().BeTrue();
    }

    [Fact]
    public void Verify_WithInvalidSignature_ReturnsFalse()
    {
        const string secret = "test-secret";
        const string payload = "{\"example\":1}";

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RESEND_WEBHOOK_SECRET"] = secret
        }).Build();

        var verifier = new ResendWebhookVerifier(config, NullLogger<ResendWebhookVerifier>.Instance);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["resend-signature"] = "deadbeef"
        };

        verifier.Verify("resend", payload, headers).Should().BeFalse();
    }

    private static string ComputeSignature(string secret, string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

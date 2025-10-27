using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Pipelane.Application.Abstractions;

namespace Pipelane.Infrastructure.Webhooks;

public sealed class ResendWebhookVerifier : IProviderWebhookVerifier
{
    private const string ProviderKey = "resend";
    private const string SignatureHeader = "resend-signature";
    private readonly ILogger<ResendWebhookVerifier> _logger;
    private readonly string? _secret;

    public ResendWebhookVerifier(IConfiguration configuration, ILogger<ResendWebhookVerifier> logger)
    {
        _logger = logger;
        _secret = configuration["RESEND_WEBHOOK_SECRET"] ?? configuration["Resend:WebhookSecret"];
    }

    /// <inheritdoc/>
    public bool Verify(string provider, string payload, IReadOnlyDictionary<string, string> headers)
    {
        if (!string.Equals(provider, ProviderKey, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(_secret))
        {
            _logger.LogWarning("Resend webhook secret is not configured");
            return false;
        }

        if (!headers.TryGetValue(SignatureHeader, out var signature) || string.IsNullOrWhiteSpace(signature))
        {
            _logger.LogWarning("Missing Resend signature header");
            return false;
        }

        var computed = ComputeSignature(payload);
        var matches = ConstantTimeEquals(signature.Trim(), computed);
        if (!matches)
        {
            _logger.LogWarning("Resend signature verification failed");
        }

        return matches;
    }

    private string ComputeSignature(string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secret!));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload ?? string.Empty));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool ConstantTimeEquals(string a, string b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        var result = 0;
        for (var i = 0; i < a.Length; i++)
        {
            result |= a[i] ^ b[i];
        }
        return result == 0;
    }
}

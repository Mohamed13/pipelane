using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Pipelane.Application.Storage;
using Pipelane.Domain.Enums;
using Pipelane.Infrastructure.Security;

namespace Pipelane.Infrastructure.Channels;

public interface IChannelConfigurationProvider
{
    Task<WhatsAppChannelConfig?> GetWhatsAppConfigAsync(Guid tenantId, CancellationToken ct);
    Task<TwilioSmsChannelConfig?> GetTwilioConfigAsync(Guid tenantId, CancellationToken ct);
}

public sealed class ChannelConfigurationProvider : IChannelConfigurationProvider
{
    private readonly IAppDbContext _db;
    private readonly IEncryptionService _encryption;
    private readonly ILogger<ChannelConfigurationProvider> _logger;

    public ChannelConfigurationProvider(
        IAppDbContext db,
        IEncryptionService encryption,
        ILogger<ChannelConfigurationProvider> logger)
    {
        _db = db;
        _encryption = encryption;
        _logger = logger;
    }

    public async Task<WhatsAppChannelConfig?> GetWhatsAppConfigAsync(Guid tenantId, CancellationToken ct)
    {
        var values = await LoadSettingsAsync(Channel.Whatsapp, tenantId, ct).ConfigureAwait(false);
        if (values is null)
        {
            return null;
        }

        if (!TryGet(values, "WA_APP_ID", out var appId)
            || !TryGet(values, "WA_APP_SECRET", out var appSecret)
            || !TryGet(values, "WA_WABA_ID", out var wabaId)
            || !TryGet(values, "WA_PHONE_NUMBER_ID", out var phoneNumberId)
            || !TryGet(values, "WA_ACCESS_TOKEN", out var accessToken)
            || !TryGet(values, "WA_VERIFY_TOKEN", out var verifyToken))
        {
            _logger.LogWarning("WhatsApp configuration incomplete for tenant {TenantId}", tenantId);
            return null;
        }

        return new WhatsAppChannelConfig(
            appId,
            appSecret,
            wabaId,
            phoneNumberId,
            accessToken,
            verifyToken);
    }

    public async Task<TwilioSmsChannelConfig?> GetTwilioConfigAsync(Guid tenantId, CancellationToken ct)
    {
        var values = await LoadSettingsAsync(Channel.Sms, tenantId, ct).ConfigureAwait(false);
        if (values is null)
        {
            return null;
        }

        if (!TryGet(values, "TWILIO_ACCOUNT_SID", out var accountSid)
            || !TryGet(values, "TWILIO_AUTH_TOKEN", out var authToken))
        {
            _logger.LogWarning("Twilio configuration missing credentials for tenant {TenantId}", tenantId);
            return null;
        }

        values.TryGetValue("TWILIO_MESSAGING_SERVICE_SID", out var messagingServiceSid);
        values.TryGetValue("TWILIO_FROM_NUMBER", out var fromNumber);

        return new TwilioSmsChannelConfig(
            accountSid,
            authToken,
            messagingServiceSid,
            fromNumber);
    }

    private async Task<Dictionary<string, string>?> LoadSettingsAsync(Channel channel, Guid tenantId, CancellationToken ct)
    {
        var record = await _db.ChannelSettings
            .AsNoTracking()
            .Where(s => s.Channel == channel && s.TenantId == tenantId)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (record is null)
        {
            return null;
        }

        try
        {
            var decrypted = _encryption.Decrypt(record.SettingsJson);
            if (string.IsNullOrWhiteSpace(decrypted))
            {
                return null;
            }

            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(decrypted, SerializerOptions);
            return dict is null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(dict, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is JsonException or FormatException or CryptographicException)
        {
            _logger.LogError(ex, "Failed to decrypt or parse channel settings for {Channel} tenant {TenantId}", channel, tenantId);
            return null;
        }
    }

    private bool TryGet(Dictionary<string, string> values, string key, out string value)
    {
        if (values.TryGetValue(key, out var raw) && !string.IsNullOrWhiteSpace(raw))
        {
            value = raw.Trim();
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
}

public sealed record WhatsAppChannelConfig(
    string AppId,
    string AppSecret,
    string WabaId,
    string PhoneNumberId,
    string AccessToken,
    string VerifyToken);

public sealed record TwilioSmsChannelConfig(
    string AccountSid,
    string AuthToken,
    string? MessagingServiceSid,
    string? FromNumber);

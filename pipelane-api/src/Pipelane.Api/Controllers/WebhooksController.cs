using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;

using Pipelane.Application.Abstractions;
using Pipelane.Domain.Enums;
using Pipelane.Infrastructure.Channels;
using Pipelane.Infrastructure.Webhooks;

namespace Pipelane.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/webhooks")]
public sealed class WebhooksController : ControllerBase
{
    private readonly IEnumerable<IMessageChannel> _channels;
    private readonly IChannelConfigurationProvider _configProvider;
    private readonly IWebhookDeadLetterStore _deadLetterStore;
    private readonly ILogger<WebhooksController> _logger;

    public WebhooksController(
        IEnumerable<IMessageChannel> channels,
        IChannelConfigurationProvider configProvider,
        IWebhookDeadLetterStore deadLetterStore,
        ILogger<WebhooksController> logger)
    {
        _channels = channels;
        _configProvider = configProvider;
        _deadLetterStore = deadLetterStore;
        _logger = logger;
    }

    [HttpGet("whatsapp")]
    public async Task<IActionResult> VerifyWhatsApp(
        [FromQuery(Name = "tenant")] Guid tenantId,
        [FromQuery(Name = "hub.challenge")] string? challenge,
        [FromQuery(Name = "hub.verify_token")] string? verifyToken,
        CancellationToken ct)
    {
        if (tenantId == Guid.Empty)
        {
            return BadRequest("tenant query parameter is required.");
        }

        var config = await _configProvider.GetWhatsAppConfigAsync(tenantId, ct).ConfigureAwait(false);
        if (config is null)
        {
            return NotFound();
        }

        if (!string.Equals(config.VerifyToken, verifyToken, StringComparison.Ordinal))
        {
            return Unauthorized();
        }

        return Content(challenge ?? "ok");
    }

    [HttpPost("whatsapp")]
    [EnableRateLimiting("webhooks")]
    public async Task<IActionResult> WhatsApp(
        [FromQuery(Name = "tenant")] Guid tenantId,
        CancellationToken ct)
    {
        var body = await ReadBodyAsync(ct).ConfigureAwait(false);
        var headers = BuildHeaders(tenantId, "whatsapp");
        return await ProcessWebhookAsync(Channel.Whatsapp, tenantId, "whatsapp", body, headers, ct).ConfigureAwait(false);
    }

    [HttpPost("sms/twilio/status")]
    [EnableRateLimiting("webhooks")]
    public async Task<IActionResult> TwilioStatus(
        [FromQuery(Name = "tenant")] Guid tenantId,
        CancellationToken ct)
    {
        var body = await ReadBodyAsync(ct).ConfigureAwait(false);
        var headers = BuildHeaders(tenantId, "status");
        return await ProcessWebhookAsync(Channel.Sms, tenantId, "status", body, headers, ct).ConfigureAwait(false);
    }

    [HttpPost("sms/twilio/inbound")]
    [EnableRateLimiting("webhooks")]
    public async Task<IActionResult> TwilioInbound(
        [FromQuery(Name = "tenant")] Guid tenantId,
        CancellationToken ct)
    {
        var body = await ReadBodyAsync(ct).ConfigureAwait(false);
        var headers = BuildHeaders(tenantId, "inbound");
        return await ProcessWebhookAsync(Channel.Sms, tenantId, "inbound", body, headers, ct).ConfigureAwait(false);
    }

    private IMessageChannel ResolveChannel(Channel channel)
        => _channels.First(c => c.Channel == channel);

    private async Task<string> ReadBodyAsync(CancellationToken ct)
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync(ct).ConfigureAwait(false);
        Request.Body.Position = 0;
        return body;
    }

    private Dictionary<string, string> BuildHeaders(Guid? tenantId, string kind)
    {
        var dict = Request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        if (tenantId.HasValue && tenantId.Value != Guid.Empty)
        {
            dict["x-tenant-id"] = tenantId.Value.ToString();
        }
        if (!string.IsNullOrWhiteSpace(kind))
        {
            dict["x-webhook-kind"] = kind;
        }

        if (!dict.ContainsKey("x-request-url"))
        {
            var url = $"{Request.Scheme}://{Request.Host}{Request.Path}{Request.QueryString}";
            dict["x-request-url"] = url;
        }

        return dict;
    }

    private async Task<IActionResult> ProcessWebhookAsync(
        Channel channelType,
        Guid queryTenantId,
        string kind,
        string payload,
        Dictionary<string, string> headers,
        CancellationToken ct)
    {
        var tenantId = ResolveTenant(queryTenantId, headers);
        var provider = channelType switch
        {
            Channel.Email => "resend",
            Channel.Sms => "twilio",
            Channel.Whatsapp => "whatsapp",
            _ => channelType.ToString().ToLowerInvariant()
        };

        var channel = ResolveChannel(channelType);

        try
        {
            var result = await channel.HandleWebhookAsync(payload, headers, ct).ConfigureAwait(false);
            if (!result.Ok)
            {
                var reason = result.Reason ?? "unknown_error";
                await _deadLetterStore.LogFailureAsync(tenantId, channelType, provider, kind, payload, headers, reason, ct).ConfigureAwait(false);
                _logger.LogWarning("Webhook enqueued for retry channel={Channel} kind={Kind} reason={Reason}", channelType, kind, reason);
                return StatusCode(StatusCodes.Status202Accepted);
            }

            return Ok();
        }
        catch (Exception ex)
        {
            await _deadLetterStore.LogFailureAsync(tenantId, channelType, provider, kind, payload, headers, ex.Message, ct).ConfigureAwait(false);
            _logger.LogError(ex, "Webhook processing crashed channel={Channel} kind={Kind}", channelType, kind);
            return StatusCode(StatusCodes.Status202Accepted);
        }
    }

    private static Guid ResolveTenant(Guid queryTenantId, IReadOnlyDictionary<string, string> headers)
    {
        if (queryTenantId != Guid.Empty)
        {
            return queryTenantId;
        }

        if (headers.TryGetValue("x-tenant-id", out var raw) && Guid.TryParse(raw, out var headerTenant))
        {
            return headerTenant;
        }

        return Guid.Empty;
    }
}

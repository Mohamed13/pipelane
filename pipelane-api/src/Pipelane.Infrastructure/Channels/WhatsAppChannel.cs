using System.Net.Http.Json;

using Microsoft.Extensions.Logging;

using Pipelane.Application.Abstractions;
using Pipelane.Domain.Entities;
using Pipelane.Domain.Enums;

namespace Pipelane.Infrastructure.Channels;

public sealed class WhatsAppChannel : IMessageChannel
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<WhatsAppChannel> _logger;
    public Channel Channel => Channel.Whatsapp;

    public WhatsAppChannel(IHttpClientFactory httpFactory, ILogger<WhatsAppChannel> logger)
    {
        _httpFactory = httpFactory; _logger = logger;
    }

    public Task<WebhookResult> HandleWebhookAsync(string body, IDictionary<string, string> headers, CancellationToken ct)
    {
        // Parse provider event minimal stub
        _logger.LogInformation("WhatsApp webhook received");
        return Task.FromResult(new WebhookResult(true, null));
    }

    public async Task<SendResult> SendTemplateAsync(Contact c, Template t, IDictionary<string, string> vars, SendMeta meta, CancellationToken ct)
    {
        // Stub: call Meta WhatsApp Cloud API if configured via ChannelSettings
        _logger.LogInformation("Sending WhatsApp template {Template} to {Phone}", t.Name, c.Phone);
        await Task.Yield();
        return new SendResult(true, Guid.NewGuid().ToString(), null);
    }

    public async Task<SendResult> SendTextAsync(Contact c, string text, SendMeta meta, CancellationToken ct)
    {
        _logger.LogInformation("Sending WhatsApp text to {Phone}", c.Phone);
        await Task.Yield();
        return new SendResult(true, Guid.NewGuid().ToString(), null);
    }

    public Task<bool> ValidateTemplateAsync(Template t, CancellationToken ct)
        => Task.FromResult(true);
}

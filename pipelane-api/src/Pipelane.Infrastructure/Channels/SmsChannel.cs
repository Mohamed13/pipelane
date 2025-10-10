using Microsoft.Extensions.Logging;

using Pipelane.Application.Abstractions;
using Pipelane.Domain.Entities;
using Pipelane.Domain.Enums;

namespace Pipelane.Infrastructure.Channels;

public sealed class SmsChannel : IMessageChannel
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<SmsChannel> _logger;
    public Channel Channel => Channel.Sms;

    public SmsChannel(IHttpClientFactory httpFactory, ILogger<SmsChannel> logger)
    {
        _httpFactory = httpFactory; _logger = logger;
    }

    public Task<WebhookResult> HandleWebhookAsync(string body, IDictionary<string, string> headers, CancellationToken ct)
    {
        _logger.LogInformation("SMS webhook received");
        return Task.FromResult(new WebhookResult(true, null));
    }

    public Task<SendResult> SendTemplateAsync(Contact c, Template t, IDictionary<string, string> vars, SendMeta meta, CancellationToken ct)
    {
        _logger.LogInformation("Sending SMS template {Template} to {Phone}", t.Name, c.Phone);
        return Task.FromResult(new SendResult(true, Guid.NewGuid().ToString(), null));
    }

    public Task<SendResult> SendTextAsync(Contact c, string text, SendMeta meta, CancellationToken ct)
    {
        _logger.LogInformation("Sending SMS text to {Phone}", c.Phone);
        return Task.FromResult(new SendResult(true, Guid.NewGuid().ToString(), null));
    }

    public Task<bool> ValidateTemplateAsync(Template t, CancellationToken ct) => Task.FromResult(true);
}

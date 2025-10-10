using Microsoft.AspNetCore.Mvc;

using Pipelane.Application.Abstractions;
using Pipelane.Domain.Enums;

namespace Pipelane.Api.Controllers;

[ApiController]
[Microsoft.AspNetCore.Authorization.AllowAnonymous]
public class WebhooksController : ControllerBase
{
    private readonly IEnumerable<IMessageChannel> _channels;
    public WebhooksController(IEnumerable<IMessageChannel> channels) => _channels = channels;

    [HttpGet("webhooks/whatsapp")]
    public IActionResult VerifyWhatsApp([FromQuery] string? hub_challenge) => Content(hub_challenge ?? "ok");

    [HttpPost("webhooks/whatsapp")]
    public async Task<IActionResult> WhatsApp(CancellationToken ct)
        => await _channels.First(c => c.Channel == Channel.Whatsapp).HandleWebhookAsync(await new StreamReader(Request.Body).ReadToEndAsync(ct), Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()), ct) is { Ok: true }
            ? Ok()
            : Problem();

    [HttpPost("webhooks/email")]
    public async Task<IActionResult> Email(CancellationToken ct)
        => await _channels.First(c => c.Channel == Channel.Email).HandleWebhookAsync(await new StreamReader(Request.Body).ReadToEndAsync(ct), Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()), ct) is { Ok: true }
            ? Ok()
            : Problem();

    [HttpPost("webhooks/sms")]
    public async Task<IActionResult> Sms(CancellationToken ct)
        => await _channels.First(c => c.Channel == Channel.Sms).HandleWebhookAsync(await new StreamReader(Request.Body).ReadToEndAsync(ct), Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()), ct) is { Ok: true }
            ? Ok()
            : Problem();
}

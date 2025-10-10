using System.IO;

using Microsoft.AspNetCore.Mvc;

using Pipelane.Application.Abstractions;
using Pipelane.Infrastructure.Webhooks;

namespace Pipelane.Api.Controllers;

[ApiController]
[Microsoft.AspNetCore.Authorization.AllowAnonymous]
[Route("api/webhooks/email/resend")]
public sealed class EmailResendWebhookController : ControllerBase
{
    private readonly IProviderWebhookVerifier _verifier;
    private readonly ResendWebhookProcessor _processor;
    private readonly ILogger<EmailResendWebhookController> _logger;

    public EmailResendWebhookController(IProviderWebhookVerifier verifier, ResendWebhookProcessor processor, ILogger<EmailResendWebhookController> logger)
    {
        _verifier = verifier;
        _processor = processor;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Handle(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        var headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase);

        if (!_verifier.Verify("resend", payload, headers))
        {
            return Unauthorized();
        }

        var processed = await _processor.ProcessAsync(payload, cancellationToken);
        if (!processed)
        {
            _logger.LogWarning("Failed to process Resend webhook payload");
            return Problem();
        }

        return Ok();
    }
}

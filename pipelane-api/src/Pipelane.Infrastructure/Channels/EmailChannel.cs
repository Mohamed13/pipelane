using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Pipelane.Application.Abstractions;
using Pipelane.Domain.Entities;
using Pipelane.Domain.Enums;

namespace Pipelane.Infrastructure.Channels;

public sealed class EmailChannel : IMessageChannel
{
    private const string ResendClientName = "Resend";
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<EmailChannel> _logger;
    private readonly string? _apiKey;
    private readonly string? _fromAddress;
    private readonly string? _fromName;

    public Channel Channel => Channel.Email;

    public EmailChannel(IHttpClientFactory httpFactory, IConfiguration configuration, ILogger<EmailChannel> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
        _apiKey = configuration["RESEND_API_KEY"] ?? configuration["Resend:ApiKey"];
        _fromAddress = configuration["RESEND_FROM_EMAIL"] ?? configuration["Resend:FromEmail"];
        _fromName = configuration["RESEND_FROM_NAME"] ?? configuration["Resend:FromName"];
    }

    public Task<WebhookResult> HandleWebhookAsync(string body, IDictionary<string, string> headers, CancellationToken ct)
        => Task.FromResult(new WebhookResult(true, null));

    public async Task<SendResult> SendTemplateAsync(Contact contact, Template template, IDictionary<string, string> variables, SendMeta meta, CancellationToken ct)
    {
        return await SendAsync(contact, template, variables, ct);
    }

    public async Task<SendResult> SendTextAsync(Contact contact, string text, SendMeta meta, CancellationToken ct)
    {
        var template = new Template { Name = "text", CoreSchemaJson = JsonSerializer.Serialize(new { subject = "Message", text }) };
        var vars = new Dictionary<string, string> { { "text", text }, { "subject", "Message" } };
        return await SendAsync(contact, template, vars, ct);
    }

    public Task<bool> ValidateTemplateAsync(Template t, CancellationToken ct) => Task.FromResult(true);

    private async Task<SendResult> SendAsync(Contact contact, Template template, IDictionary<string, string> variables, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_apiKey) || string.IsNullOrWhiteSpace(_fromAddress))
        {
            _logger.LogError("Resend API key or from address is not configured");
            return new SendResult(false, null, "Email channel is not configured");
        }

        if (string.IsNullOrWhiteSpace(contact.Email))
        {
            return new SendResult(false, null, "Contact email missing");
        }

        var templateKey = ExtractTemplateKey(template);
        if (string.IsNullOrWhiteSpace(templateKey))
        {
            _logger.LogWarning("Template {Template} missing templateKey in CoreSchemaJson", template.Name);
            templateKey = template.Name;
        }

        var subject = variables.TryGetValue("subject", out var subj) && !string.IsNullOrWhiteSpace(subj)
            ? subj
            : $"Message from Pipelane";

        var payload = new ResendSendRequest
        {
            From = string.IsNullOrWhiteSpace(_fromName) ? _fromAddress! : $"{_fromName} <{_fromAddress}>",
            To = new[] { contact.Email },
            TemplateId = templateKey,
            Subject = subject,
            Variables = variables
        };

        try
        {
            var client = _httpFactory.CreateClient(ResendClientName);
            using var request = new HttpRequestMessage(HttpMethod.Post, "emails")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions.Value), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            using var response = await client.SendAsync(request, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Resend send failed ({Status}): {Body}", response.StatusCode, responseBody);
                return new SendResult(false, null, responseBody);
            }

            try
            {
                var doc = JsonDocument.Parse(responseBody);
                var providerId = doc.RootElement.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                return new SendResult(true, providerId, null);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse Resend response: {Body}", responseBody);
                return new SendResult(true, null, null);
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "Resend send failed");
            return new SendResult(false, null, ex.Message);
        }
    }

    private static string? ExtractTemplateKey(Template template)
    {
        if (string.IsNullOrWhiteSpace(template.CoreSchemaJson))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(template.CoreSchemaJson);
            if (doc.RootElement.TryGetProperty("templateKey", out var key) && key.ValueKind == JsonValueKind.String)
            {
                return key.GetString();
            }
            if (doc.RootElement.TryGetProperty("resendTemplateId", out var id) && id.ValueKind == JsonValueKind.String)
            {
                return id.GetString();
            }
        }
        catch (JsonException)
        {
            // ignore malformed schema
        }

        return null;
    }

    private static readonly Lazy<JsonSerializerOptions> JsonOptions = new(() => new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    });

    private sealed class ResendSendRequest
    {
        public string From { get; set; } = string.Empty;
        public string[] To { get; set; } = Array.Empty<string>();
        public string TemplateId { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public IDictionary<string, string> Variables { get; set; } = new Dictionary<string, string>();
    }
}

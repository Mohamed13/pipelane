using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Pipelane.Infrastructure.Automations;

public interface IAutomationEventPublisher
{
    Task PublishAsync(string eventType, object payload, Guid tenantId, CancellationToken ct);
}

public sealed class AutomationEventPublisher : IAutomationEventPublisher
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<AutomationsOptions> _options;
    private readonly ILogger<AutomationEventPublisher> _logger;

    public AutomationEventPublisher(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<AutomationsOptions> options,
        ILogger<AutomationEventPublisher> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task PublishAsync(string eventType, object payload, Guid tenantId, CancellationToken ct)
    {
        var settings = _options.CurrentValue;
        if (!settings.EventsEnabled || string.IsNullOrWhiteSpace(settings.EventsUrl))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.Token))
        {
            _logger.LogDebug("Automations token missing; skipping event {EventType}", eventType);
            return;
        }

        try
        {
            var body = JsonSerializer.Serialize(new
            {
                eventType,
                tenantId,
                occurredAt = DateTime.UtcNow,
                payload
            }, SerializerOptions);

            var client = _httpClientFactory.CreateClient("Automations");
            using var request = new HttpRequestMessage(HttpMethod.Post, settings.EventsUrl)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.Token);
            request.Headers.Add("X-Automations-Token", settings.Token);

            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var status = (int)response.StatusCode;
                var reason = response.ReasonPhrase ?? "unknown";
                _logger.LogWarning("Automations webhook returned {Status}: {Reason}", status, reason);
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Automations webhook timed out for event {EventType}", eventType);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Automations webhook failed for event {EventType}", eventType);
        }
    }
}

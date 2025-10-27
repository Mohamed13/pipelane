using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Pipelane.Application.DTOs;
using Pipelane.Application.Services;
using Pipelane.Application.Storage;
using Pipelane.Domain.Entities;
using Pipelane.Domain.Enums;
using Pipelane.Infrastructure.Automations;
using Pipelane.Infrastructure.Persistence;

namespace Pipelane.Api.Controllers;

[ApiController]
[Route("api/automations/actions")]
[EnableRateLimiting("automations")]
public sealed class AutomationsController : ControllerBase
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    private readonly IOptionsMonitor<AutomationsOptions> _options;
    private readonly IMessagingService _messaging;
    private readonly IOutboxService _outbox;
    private readonly IAppDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<AutomationsController> _logger;

    public AutomationsController(
        IOptionsMonitor<AutomationsOptions> options,
        IMessagingService messaging,
        IOutboxService outbox,
        IAppDbContext db,
        ITenantProvider tenantProvider,
        ILogger<AutomationsController> logger)
    {
        _options = options;
        _messaging = messaging;
        _outbox = outbox;
        _db = db;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Handle([FromBody] AutomationActionRequest request, CancellationToken ct)
    {
        var settings = _options.CurrentValue;
        if (!settings.ActionsEnabled)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Title = "Automations disabled",
                Detail = "Inbound automations are currently disabled."
            });
        }

        if (string.IsNullOrWhiteSpace(settings.Token))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Title = "Automations token missing",
                Detail = "Configure AUTOMATIONS_TOKEN to process requests."
            });
        }

        if (!Request.Headers.TryGetValue("X-Automations-Token", out var token) ||
            !string.Equals(token, settings.Token, StringComparison.Ordinal))
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Unauthorized",
                Detail = "Missing or invalid X-Automations-Token header."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Type))
        {
            return BadRequest(new ProblemDetails { Title = "type required" });
        }

        return request.Type.ToLowerInvariant() switch
        {
            "send_message" => await HandleSendMessageAsync(request, ct),
            "create_task" => await HandleCreateTaskAsync(request, ct),
            "schedule_followup" => await HandleScheduleFollowupAsync(request, ct),
            _ => BadRequest(new ProblemDetails { Title = "Unsupported type", Detail = request.Type })
        };
    }

    private async Task<IActionResult> HandleSendMessageAsync(AutomationActionRequest request, CancellationToken ct)
    {
        var payload = Deserialize<SendMessageActionData>(request.Data);
        if (payload is null || payload.ContactId == Guid.Empty)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid send_message payload" });
        }

        var mode = string.Equals(payload.Mode, "template", StringComparison.OrdinalIgnoreCase) ? "template" : "text";
        if (mode == "text" && string.IsNullOrWhiteSpace(payload.Text))
        {
            return BadRequest(new ProblemDetails { Title = "Text is required for send_message text mode" });
        }
        if (mode == "template" && string.IsNullOrWhiteSpace(payload.TemplateName))
        {
            return BadRequest(new ProblemDetails { Title = "TemplateName is required for template mode" });
        }

        var requestDto = new SendMessageRequest(
            payload.ContactId,
            payload.Phone,
            payload.Channel,
            mode,
            payload.Text,
            payload.TemplateName,
            payload.Language,
            payload.Variables,
            null);

        var result = await _messaging.SendAsync(requestDto, ct);
        if (!result.Success)
        {
            return UnprocessableEntity(new ProblemDetails { Title = "Send failed", Detail = result.Error });
        }

        return Ok(new { ok = true, providerMessageId = result.ProviderMessageId });
    }

    private async Task<IActionResult> HandleCreateTaskAsync(AutomationActionRequest request, CancellationToken ct)
    {
        var payload = Deserialize<CreateTaskActionData>(request.Data);
        if (payload is null || payload.ContactId == Guid.Empty || string.IsNullOrWhiteSpace(payload.Title))
        {
            return BadRequest(new ProblemDetails { Title = "Invalid create_task payload" });
        }

        var contact = await FindContactAsync(payload.ContactId, ct);
        if (contact is null)
        {
            return NotFound(new ProblemDetails { Title = "Contact not found" });
        }

        var task = new FollowupTask
        {
            Id = Guid.NewGuid(),
            TenantId = contact.TenantId,
            ContactId = contact.Id,
            MessageId = payload.MessageId,
            Title = payload.Title,
            Notes = payload.Notes,
            DueAtUtc = payload.DueAtUtc,
            CreatedAtUtc = DateTime.UtcNow,
            Completed = false
        };

        _db.FollowupTasks.Add(task);
        await _db.SaveChangesAsync(ct);

        return Ok(new { ok = true, taskId = task.Id });
    }

    private async Task<IActionResult> HandleScheduleFollowupAsync(AutomationActionRequest request, CancellationToken ct)
    {
        var payload = Deserialize<ScheduleFollowupActionData>(request.Data);
        if (payload is null || payload.ContactId == Guid.Empty)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid schedule_followup payload" });
        }

        var contact = await FindContactAsync(payload.ContactId, ct);
        if (contact is null)
        {
            return NotFound(new ProblemDetails { Title = "Contact not found" });
        }

        var mode = string.Equals(payload.Mode, "template", StringComparison.OrdinalIgnoreCase) ? "template" : "text";
        var tenantId = contact.TenantId;

        Guid? templateId = payload.TemplateId;
        if (mode == "template")
        {
            if (templateId is null && !string.IsNullOrWhiteSpace(payload.TemplateName))
            {
                var template = await _db.Templates
                    .Where(t => t.TenantId == tenantId && t.Channel == payload.Channel && t.Name == payload.TemplateName)
                    .OrderByDescending(t => t.UpdatedAtUtc)
                    .FirstOrDefaultAsync(ct);

                if (template is null)
                {
                    return BadRequest(new ProblemDetails { Title = "Template not found" });
                }

                templateId = template.Id;
            }

            if (templateId is null)
            {
                return BadRequest(new ProblemDetails { Title = "TemplateId or TemplateName required for template mode" });
            }
        }

        var outbox = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ContactId = contact.Id,
            Channel = payload.Channel,
            Type = mode == "template" ? MessageType.Template : MessageType.Text,
            TemplateId = templateId,
            PayloadJson = BuildPayloadJson(mode, payload),
            ScheduledAtUtc = payload.ScheduledAtUtc,
            CreatedAt = DateTime.UtcNow
        };

        await _outbox.EnqueueAsync(outbox, ct);
        _logger.LogInformation("Scheduled follow-up message {MessageId} for contact {ContactId} at {ScheduledAt}", outbox.Id, contact.Id, payload.ScheduledAtUtc);

        return Ok(new { ok = true, scheduledAt = payload.ScheduledAtUtc, messageId = outbox.Id });
    }

    private static string BuildPayloadJson(string mode, ScheduleFollowupActionData payload)
    {
        if (mode == "template")
        {
            return JsonSerializer.Serialize(payload.Variables ?? new Dictionary<string, string>(), SerializerOptions);
        }

        return JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["text"] = payload.Text ?? string.Empty
        }, SerializerOptions);
    }

    private async Task<Contact?> FindContactAsync(Guid contactId, CancellationToken ct)
    {
        var tenantId = _tenantProvider.TenantId;
        return await _db.Contacts
            .Where(c => c.Id == contactId && c.TenantId == tenantId)
            .FirstOrDefaultAsync(ct);
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    private static T? Deserialize<T>(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Undefined || element.ValueKind == JsonValueKind.Null)
        {
            return default;
        }
        return element.Deserialize<T>(SerializerOptions);
    }

    public sealed record AutomationActionRequest
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = string.Empty;

        [JsonPropertyName("data")]
        public JsonElement Data { get; init; }
    }

    public sealed record SendMessageActionData
    {
        [JsonPropertyName("contactId")]
        public Guid ContactId { get; init; }

        [JsonPropertyName("phone")]
        public string? Phone { get; init; }

        [JsonPropertyName("channel")]
        public Channel Channel { get; init; }

        [JsonPropertyName("mode")]
        public string Mode { get; init; } = "text";

        [JsonPropertyName("text")]
        public string? Text { get; init; }

        [JsonPropertyName("templateName")]
        public string? TemplateName { get; init; }

        [JsonPropertyName("language")]
        public string? Language { get; init; }

        [JsonPropertyName("variables")]
        public Dictionary<string, string>? Variables { get; init; }
    }

    public sealed record CreateTaskActionData
    {
        [JsonPropertyName("contactId")]
        public Guid ContactId { get; init; }

        [JsonPropertyName("messageId")]
        public Guid? MessageId { get; init; }

        [JsonPropertyName("title")]
        public string Title { get; init; } = string.Empty;

        [JsonPropertyName("dueAtUtc")]
        public DateTime DueAtUtc { get; init; }

        [JsonPropertyName("notes")]
        public string? Notes { get; init; }
    }

    public sealed record ScheduleFollowupActionData
    {
        [JsonPropertyName("contactId")]
        public Guid ContactId { get; init; }

        [JsonPropertyName("channel")]
        public Channel Channel { get; init; }

        [JsonPropertyName("scheduledAtUtc")]
        public DateTime ScheduledAtUtc { get; init; }

        [JsonPropertyName("mode")]
        public string Mode { get; init; } = "text";

        [JsonPropertyName("text")]
        public string? Text { get; init; }

        [JsonPropertyName("templateId")]
        public Guid? TemplateId { get; init; }

        [JsonPropertyName("templateName")]
        public string? TemplateName { get; init; }

        [JsonPropertyName("variables")]
        public Dictionary<string, string>? Variables { get; init; }
    }
}

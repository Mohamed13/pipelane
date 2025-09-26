using Pipelane.Domain.Entities;
using Pipelane.Domain.Enums;

namespace Pipelane.Application.Abstractions;

public record SendMeta(Guid? ConversationId, string? ProviderHint);
public record SendResult(bool Success, string? ProviderMessageId, string? Error);
public record WebhookResult(bool Ok, string? Reason);

public interface IMessageChannel
{
    Channel Channel { get; }
    Task<SendResult> SendTextAsync(Contact c, string text, SendMeta meta, CancellationToken ct);
    Task<SendResult> SendTemplateAsync(Contact c, Template t, IDictionary<string,string> vars, SendMeta meta, CancellationToken ct);
    Task<WebhookResult> HandleWebhookAsync(string body, IDictionary<string,string> headers, CancellationToken ct);
    Task<bool> ValidateTemplateAsync(Template t, CancellationToken ct);
}

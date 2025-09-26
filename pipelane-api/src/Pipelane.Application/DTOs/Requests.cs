using Pipelane.Domain.Enums;

namespace Pipelane.Application.DTOs;

public record ChannelSettingsDto(Channel Channel, Dictionary<string,string> Settings);

public record SendMessageRequest(
    Guid? ContactId,
    string? Phone,
    Channel Channel,
    string Type,
    string? Text,
    string? TemplateName,
    string? Lang,
    Dictionary<string,string>? Variables,
    Dictionary<string,string>? Meta);

public record CreateCampaignRequest(
    string Name,
    Channel PrimaryChannel,
    string? FallbackOrderJson,
    Guid TemplateId,
    string SegmentJson,
    DateTime? ScheduledAtUtc);

public record ImportContactsRequest(string Kind, string PayloadBase64);

public record ConversionRequest(Guid ContactId, decimal Amount, string Currency, string? OrderId);


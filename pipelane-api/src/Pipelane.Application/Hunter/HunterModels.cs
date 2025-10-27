using System;
using System.Collections.Generic;

using Pipelane.Domain.Entities.Prospecting;

namespace Pipelane.Application.Hunter;

public record HunterSearchCriteria(
    string? Industry,
    GeoCriteria? Geo,
    HunterFilters? Filters,
    string? Source,
    string? TextQuery,
    Guid? CsvId);

public record GeoCriteria(double Lat, double Lng, double RadiusKm);

public record HunterFilters(
    int? ReviewsMin,
    string? PriceBand,
    bool? HasSite,
    bool? Booking,
    bool? SocialActive,
    double? RatingMin);

public record HunterProspectDto(
    string? FirstName,
    string? LastName,
    string? Company,
    string? Email,
    string? Phone,
    string? WhatsAppMsisdn,
    string? Website,
    string? City,
    string? Country,
    ProspectSocialDto? Social);

public record ProspectSocialDto(string? Instagram, string? LinkedIn, string? Facebook);

public record HunterFeaturesDto(
    double? Rating,
    int? Reviews,
    bool? HasSite,
    bool? Booking,
    bool? SocialActive,
    string? Cms,
    bool? MobileOk,
    bool? PixelPresent,
    bool? LcpSlow);

public record HunterResultDto(
    Guid ProspectId,
    HunterProspectDto Prospect,
    HunterFeaturesDto Features,
    int Score,
    IReadOnlyList<string>? Why);

public record HunterSearchResponse(int Total, int Duplicates, IReadOnlyList<HunterResultDto>? Items);

public record CreateListRequest(string? Name);

public record AddToListRequest(IReadOnlyList<Guid>? ProspectIds);

public record AddToListResponse(int Added, int Skipped);

public record ProspectListSummary(Guid Id, string? Name, int Count, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);

public record ProspectListResponse(
    Guid Id,
    string? Name,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyList<ProspectListItemResponse>? Items);

public record ProspectListItemResponse(
    Guid ProspectId,
    HunterProspectDto Prospect,
    int Score,
    HunterFeaturesDto Features,
    IReadOnlyList<string>? Why,
    DateTime AddedAtUtc);

public record CadenceFromListRequest(
    Guid ListId,
    string? Name,
    int? DailyCap,
    string? Window,
    IReadOnlyList<CadenceStepRequest>? Steps);

public record CadenceStepRequest(
    int OffsetDays,
    string Channel,
    string? TemplateId,
    string? Prompt);

public record RenameListRequest(string? Name);



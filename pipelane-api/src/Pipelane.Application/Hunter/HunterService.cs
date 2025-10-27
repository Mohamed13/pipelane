using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Pipelane.Application.Hunter;
using Pipelane.Application.Services;
using Pipelane.Application.Storage;
using Pipelane.Domain.Entities.Prospecting;
using Pipelane.Domain.Enums;
using Pipelane.Domain.Enums.Prospecting;

namespace Pipelane.Application.Hunter;

public interface IHunterService
{
    Task<Guid> UploadCsvAsync(Guid tenantId, StreamReference file, CancellationToken ct);
    Task<HunterSearchResponse> SearchAsync(Guid tenantId, HunterSearchCriteria criteria, bool dryRun, CancellationToken ct);
    Task<Guid> CreateListAsync(Guid tenantId, CreateListRequest request, CancellationToken ct);
    Task<AddToListResponse> AddToListAsync(Guid tenantId, Guid listId, AddToListRequest request, CancellationToken ct);
    Task<ProspectListResponse> GetListAsync(Guid tenantId, Guid listId, CancellationToken ct);
    Task<IReadOnlyList<ProspectListSummary>> GetListsAsync(Guid tenantId, CancellationToken ct);
    Task RenameListAsync(Guid tenantId, Guid listId, RenameListRequest request, CancellationToken ct);
    Task DeleteListAsync(Guid tenantId, Guid listId, CancellationToken ct);
    Task<Guid> CreateCadenceFromListAsync(Guid tenantId, CadenceFromListRequest request, CancellationToken ct);
}

public sealed record StreamReference(string FileName, Stream Content);

public sealed class HunterService : IHunterService
{
    private readonly IAppDbContext _db;
    private readonly IEnumerable<ILeadProvider> _providers;
    private readonly IHunterEnrichService _enrich;
    private readonly IHunterScoreService _score;
    private readonly IWhyThisLeadService _why;
    private readonly IHunterCsvStore _csvStore;
    private readonly IMessagingLimitsProvider _limitsProvider;
    private readonly ILogger<HunterService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public HunterService(
        IAppDbContext db,
        IEnumerable<ILeadProvider> providers,
        IHunterEnrichService enrich,
        IHunterScoreService score,
        IWhyThisLeadService why,
        IHunterCsvStore csvStore,
        IMessagingLimitsProvider limitsProvider,
        ILogger<HunterService> logger)
    {
        _db = db;
        _providers = providers;
        _enrich = enrich;
        _score = score;
        _why = why;
        _csvStore = csvStore;
        _limitsProvider = limitsProvider;
        _logger = logger;
    }

    public async Task<Guid> UploadCsvAsync(Guid tenantId, StreamReference file, CancellationToken ct)
    {
        if (file.Content == null) throw new ArgumentNullException(nameof(file.Content));
        var csvId = await _csvStore.SaveAsync(tenantId, file.Content, ct);
        _logger.LogInformation("Stored hunter CSV {CsvId} for tenant {Tenant}", csvId, tenantId);
        return csvId;
    }

    public async Task<HunterSearchResponse> SearchAsync(Guid tenantId, HunterSearchCriteria criteria, bool dryRun, CancellationToken ct)
    {
        var provider = ResolveProvider(criteria.Source);
        var candidates = await provider.SearchAsync(tenantId, criteria, ct);

        var total = candidates.Count;
        var now = DateTime.UtcNow;

        var normalizedEmail = new Dictionary<string, Prospect>();
        var normalizedCompanyCity = new Dictionary<string, Prospect>();

        // Seed lookup with existing prospects
        if (candidates.Count > 0)
        {
            var emails = candidates
                .Select(c => NormalizeKey(c.Prospect.Email))
                .Where(k => k != null)
                .Select(k => k!)
                .Distinct()
                .ToList();

            var fuzzyKeys = candidates
                .Where(c => string.IsNullOrWhiteSpace(c.Prospect.Email))
                .Select(c => NormalizeCompanyCity(c.Prospect.Company, c.Prospect.City))
                .Where(k => k != null)
                .Select(k => k!)
                .Distinct()
                .ToList();

            if (emails.Count > 0 || fuzzyKeys.Count > 0)
            {
                var existing = await _db.Prospects
                    .Where(p =>
                        (p.Email != null && emails.Contains(p.Email.ToLower())) ||
                        fuzzyKeys.Contains(NormalizeCompanyCity(p.Company, p.City)!))
                    .ToListAsync(ct);

                foreach (var prospect in existing)
                {
                    if (!string.IsNullOrWhiteSpace(prospect.Email))
                    {
                        var key = NormalizeKey(prospect.Email);
                        if (key != null && !normalizedEmail.ContainsKey(key))
                        {
                            normalizedEmail[key] = prospect;
                        }
                    }

                    var fuzzy = NormalizeCompanyCity(prospect.Company, prospect.City);
                    if (fuzzy != null && !normalizedCompanyCity.ContainsKey(fuzzy))
                    {
                        normalizedCompanyCity[fuzzy] = prospect;
                    }
                }
            }
        }

        var results = new List<HunterResultDto>();
        var duplicates = 0;

        foreach (var candidate in candidates)
        {
            ct.ThrowIfCancellationRequested();

            var emailKey = NormalizeKey(candidate.Prospect.Email);
            var fuzzyKey = NormalizeCompanyCity(candidate.Prospect.Company, candidate.Prospect.City);

            var existing = emailKey != null && normalizedEmail.TryGetValue(emailKey, out var byEmail)
                ? byEmail
                : (fuzzyKey != null && normalizedCompanyCity.TryGetValue(fuzzyKey, out var byCompany)
                    ? byCompany
                    : null);

            if (existing != null)
            {
                duplicates++;
                continue;
            }

            Prospect? entity = existing;
            var enriched = await _enrich.EnrichAsync(candidate.Prospect, criteria.Filters, candidate.Features, ct);
            var score = _score.ComputeScore(enriched, criteria.Filters);
            var why = _why.BuildReasons(candidate.Prospect, enriched, score);

            if (!dryRun)
            {
                if (entity is null)
                {
                    entity = new Prospect
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        Email = candidate.Prospect.Email ?? string.Empty,
                        CreatedAtUtc = now,
                        Status = ProspectStatus.New
                    };

                    await _db.Prospects.AddAsync(entity, ct);

                    if (emailKey != null) normalizedEmail[emailKey] = entity;
                    if (fuzzyKey != null) normalizedCompanyCity[fuzzyKey] = entity;
                }

                entity.FirstName = candidate.Prospect.FirstName ?? entity.FirstName;
                entity.LastName = candidate.Prospect.LastName ?? entity.LastName;
                entity.Company = candidate.Prospect.Company ?? entity.Company;
                entity.Phone = candidate.Prospect.Phone ?? entity.Phone;
                entity.City = candidate.Prospect.City ?? entity.City;
                entity.Country = candidate.Prospect.Country ?? entity.Country;
                entity.Website = candidate.Prospect.Website ?? entity.Website;
                entity.Source = criteria.Source ?? provider.Source;
                entity.Industry = criteria.Industry ?? entity.Industry;
                entity.UpdatedAtUtc = now;
                entity.EnrichedJson = JsonSerializer.Serialize(enriched, JsonOptions);

                var scoreEntity = await _db.ProspectScores
                    .FirstOrDefaultAsync(s => s.ProspectId == entity.Id, ct);

                if (scoreEntity == null)
                {
                    scoreEntity = new ProspectScore
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        ProspectId = entity.Id,
                        UpdatedAtUtc = now,
                        Score = score,
                        FeaturesJson = JsonSerializer.Serialize(new { enriched, why }, JsonOptions)
                    };
                    await _db.ProspectScores.AddAsync(scoreEntity, ct);
                }
                else
                {
                    scoreEntity.Score = score;
                    scoreEntity.FeaturesJson = JsonSerializer.Serialize(new { enriched, why }, JsonOptions);
                    scoreEntity.UpdatedAtUtc = now;
                }
            }

            var prospectId = entity?.Id ?? Guid.Empty;
            results.Add(new HunterResultDto(prospectId, candidate.Prospect, enriched, score, why));
        }

        if (!dryRun)
        {
            await _db.SaveChangesAsync(ct);
        }

        return new HunterSearchResponse(total, duplicates, results);
    }

    public async Task<Guid> CreateListAsync(Guid tenantId, CreateListRequest request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var list = new ProspectList
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = request.Name,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _db.ProspectLists.AddAsync(list, ct);
        await _db.SaveChangesAsync(ct);
        return list.Id;
    }

    public async Task<AddToListResponse> AddToListAsync(Guid tenantId, Guid listId, AddToListRequest request, CancellationToken ct)
    {
        var list = await _db.ProspectLists.FirstOrDefaultAsync(l => l.Id == listId && l.TenantId == tenantId, ct);
        if (list is null) throw new KeyNotFoundException("Liste introuvable.");

        var prospectIds = request.ProspectIds?.Distinct().ToList() ?? new List<Guid>();
        if (prospectIds.Count == 0) return new AddToListResponse(0, 0);

        var existingItems = await _db.ProspectListItems
            .Where(i => i.ProspectListId == listId && prospectIds.Contains(i.ProspectId))
            .Select(i => i.ProspectId)
            .ToListAsync(ct);

        var newIds = prospectIds.Except(existingItems).ToList();
        var now = DateTime.UtcNow;

        foreach (var prospectId in newIds)
        {
            await _db.ProspectListItems.AddAsync(new ProspectListItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProspectListId = listId,
                ProspectId = prospectId,
                AddedAtUtc = now
            }, ct);
        }

        if (newIds.Count > 0)
        {
            list.UpdatedAtUtc = now;
        }

        await _db.SaveChangesAsync(ct);
        return new AddToListResponse(newIds.Count, existingItems.Count);
    }

    public async Task<ProspectListResponse> GetListAsync(Guid tenantId, Guid listId, CancellationToken ct)
    {
        var list = await _db.ProspectLists
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == listId && l.TenantId == tenantId, ct);
        if (list is null) throw new KeyNotFoundException("Liste introuvable.");

        var items = await _db.ProspectListItems
            .Where(i => i.ProspectListId == listId)
            .Include(i => i.Prospect!)
            .ThenInclude(p => p.Score)
            .OrderByDescending(i => i.AddedAtUtc)
            .ToListAsync(ct);

        var responses = new List<ProspectListItemResponse>(items.Count);

        foreach (var item in items)
        {
            if (item.Prospect is null) continue;
            var features = item.Prospect.Score != null
                ? DeserializeFeatures(item.Prospect.Score.FeaturesJson)
                : new HunterFeaturesDto(null, null, null, null, null, null, null, null, null);
            var why = item.Prospect.Score != null
                ? DeserializeWhy(item.Prospect.Score.FeaturesJson)
                : Array.Empty<string>();

            responses.Add(new ProspectListItemResponse(
                item.ProspectId,
                new HunterProspectDto(
                    item.Prospect.FirstName,
                    item.Prospect.LastName,
                    item.Prospect.Company,
                    string.IsNullOrWhiteSpace(item.Prospect.Email) ? null : item.Prospect.Email,
                    item.Prospect.Phone,
                    null,
                    item.Prospect.Website,
                    item.Prospect.City,
                    item.Prospect.Country,
                    null),
                item.Prospect.Score?.Score ?? 0,
                features,
                why,
                item.AddedAtUtc));
        }

        return new ProspectListResponse(list.Id, list.Name, list.CreatedAtUtc, list.UpdatedAtUtc, responses);
    }

    public async Task<IReadOnlyList<ProspectListSummary>> GetListsAsync(Guid tenantId, CancellationToken ct)
    {
        var lists = await _db.ProspectLists
            .AsNoTracking()
            .Where(l => l.TenantId == tenantId)
            .Select(l => new ProspectListSummary(
                l.Id,
                l.Name,
                _db.ProspectListItems.Count(i => i.ProspectListId == l.Id),
                l.CreatedAtUtc,
                l.UpdatedAtUtc))
            .OrderByDescending(l => l.UpdatedAtUtc)
            .ToListAsync(ct);

        return lists;
    }

    public async Task RenameListAsync(Guid tenantId, Guid listId, RenameListRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Le nom de liste est obligatoire.", nameof(request));
        }

        var trimmed = request.Name.Trim();
        var exists = await _db.ProspectLists
            .AnyAsync(l => l.TenantId == tenantId && l.Id != listId && l.Name == trimmed, ct);
        if (exists)
        {
            throw new InvalidOperationException("Une liste avec ce nom existe déjà.");
        }

        var list = await _db.ProspectLists.FirstOrDefaultAsync(l => l.Id == listId && l.TenantId == tenantId, ct);
        if (list is null)
        {
            throw new KeyNotFoundException("Liste introuvable.");
        }

        list.Name = trimmed;
        list.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteListAsync(Guid tenantId, Guid listId, CancellationToken ct)
    {
        var list = await _db.ProspectLists.FirstOrDefaultAsync(l => l.Id == listId && l.TenantId == tenantId, ct);
        if (list is null)
        {
            throw new KeyNotFoundException("Liste introuvable.");
        }

        _db.ProspectLists.Remove(list);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<Guid> CreateCadenceFromListAsync(Guid tenantId, CadenceFromListRequest request, CancellationToken ct)
    {
        var list = await _db.ProspectLists.FirstOrDefaultAsync(l => l.Id == request.ListId && l.TenantId == tenantId, ct);
        if (list is null) throw new KeyNotFoundException("Liste introuvable.");

        var now = DateTime.UtcNow;
        var sequence = new ProspectingSequence
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = request.Name ?? $"Cadence {list.Name}",
            Description = "Cadence générée depuis Lead Hunter",
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Steps = new List<ProspectingSequenceStep>()
        };

        var stepsInput = request.Steps?.Count > 0 ? request.Steps : DefaultSteps();
        var order = 0;
        foreach (var step in stepsInput)
        {
            var channel = ParseChannel(step.Channel);
            sequence.Steps.Add(new ProspectingSequenceStep
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SequenceId = sequence.Id,
                Order = order++,
                StepType = SequenceStepType.Email,
                Channel = channel,
                OffsetDays = step.OffsetDays,
                PromptTemplate = step.Prompt,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }

        var limits = _limitsProvider.GetLimits();
        var settings = new
        {
            dailyCap = request.DailyCap ?? limits.DailySendCap,
            quietHours = new { start = limits.QuietHoursStart, end = limits.QuietHoursEnd },
            window = request.Window
        };

        var campaign = new ProspectingCampaign
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = request.Name ?? $"Cadence {list.Name}",
            SequenceId = sequence.Id,
            SegmentJson = JsonSerializer.Serialize(new { listId = list.Id }, JsonOptions),
            SettingsJson = JsonSerializer.Serialize(settings, JsonOptions),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Status = ProspectingCampaignStatus.Draft
        };

        await _db.ProspectingSequences.AddAsync(sequence, ct);
        await _db.ProspectingCampaigns.AddAsync(campaign, ct);
        await _db.SaveChangesAsync(ct);

        return campaign.Id;
    }

    private ILeadProvider ResolveProvider(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return _providers.FirstOrDefault(p => p.Source.Equals("mapsStub", StringComparison.OrdinalIgnoreCase))
                ?? _providers.First();
        }

        var provider = _providers.FirstOrDefault(p => p.Source.Equals(source, StringComparison.OrdinalIgnoreCase));
        if (provider is null)
        {
            throw new InvalidOperationException($"Provider '{source}' non supporté.");
        }

        return provider;
    }

    private static string? NormalizeKey(string? input)
        => string.IsNullOrWhiteSpace(input) ? null : input.Trim().ToLowerInvariant();

    private static string? NormalizeCompanyCity(string? company, string? city)
    {
        if (string.IsNullOrWhiteSpace(company)) return null;
        return $"{company.Trim().ToLowerInvariant()}|{(city ?? string.Empty).Trim().ToLowerInvariant()}";
    }

    private static HunterFeaturesDto DeserializeFeatures(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("enriched", out var enriched))
            {
                return JsonSerializer.Deserialize<HunterFeaturesDto>(enriched.GetRawText(), JsonOptions)
                    ?? new HunterFeaturesDto(null, null, null, null, null, null, null, null, null);
            }
        }
        catch
        {
            // ignore
        }

        return new HunterFeaturesDto(null, null, null, null, null, null, null, null, null);
    }

    private static string[] DeserializeWhy(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("why", out var why) && why.ValueKind == JsonValueKind.Array)
            {
                return why.EnumerateArray()
                    .Select(x => x.GetString())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s!)
                    .ToArray();
            }
        }
        catch
        {
            // ignore
        }

        return Array.Empty<string>();
    }

    private static IReadOnlyList<CadenceStepRequest> DefaultSteps() =>
        new[]
        {
            new CadenceStepRequest(0, "email", null, "Email d'introduction"),
            new CadenceStepRequest(1, "whatsapp", null, "Message WhatsApp court"),
            new CadenceStepRequest(3, "email", null, "Relance personnalisée"),
            new CadenceStepRequest(7, "sms", null, "Rappel SMS 1 ligne")
        };

    private static Channel ParseChannel(string channel) =>
        channel.ToLowerInvariant() switch
        {
            "email" => Channel.Email,
            "sms" => Channel.Sms,
            "whatsapp" or "wa" => Channel.Whatsapp,
            _ => Channel.Email
        };
}

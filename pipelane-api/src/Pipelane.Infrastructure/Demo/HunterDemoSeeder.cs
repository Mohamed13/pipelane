using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Pipelane.Application.Hunter;
using Pipelane.Application.Services;
using Pipelane.Application.Storage;
using Pipelane.Domain.Entities.Prospecting;
using Pipelane.Domain.Enums.Prospecting;

namespace Pipelane.Infrastructure.Demo;

public sealed class HunterDemoSeeder : IHunterDemoSeeder
{
    private readonly IAppDbContext _db;
    private readonly TimeProvider _clock;
    private readonly IOptions<DemoOptions> _options;
    private readonly ILogger<HunterDemoSeeder> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HunterScoreService ScoreService = new();
    private static readonly WhyThisLeadService WhyService = new();

    private static readonly string[] RestaurantPrefixes = { "Maison", "Bistro", "Atelier", "Table", "Cantine", "Brasserie", "Clos", "Epicéa", "Savoure", "Douceur" };
    private static readonly string[] RestaurantSuffixes = { "Nova", "Lumen", "Basilic", "Céleste", "Gourmande", "Riviera", "Parnasse", "Citron", "Velours", "Horizon" };
    private static readonly string[] ServicePrefixes = { "Studio", "Agence", "Collectif", "Atelier", "Bureau", "Cabinet", "Sprint", "Pixel", "Data", "Signal" };
    private static readonly string[] ServiceSuffixes = { "Bold", "Nova", "Astre", "Loop", "Craft", "Pulse", "Helix", "Bridge", "Shift", "Spark" };
    private static readonly string[] Cities = { "Paris", "Lyon", "Marseille", "Toulouse", "Bordeaux", "Nantes", "Montpellier", "Lille", "Rennes", "Strasbourg" };

    public HunterDemoSeeder(IAppDbContext db, TimeProvider? clock, IOptions<DemoOptions> options, ILogger<HunterDemoSeeder> logger)
    {
        _db = db;
        _clock = clock ?? TimeProvider.System;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<HunterResultDto>> SeedAsync(Guid tenantId, CancellationToken ct)
    {
        if (!_options.Value.Enabled)
        {
            throw new InvalidOperationException("Demo mode is disabled.");
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var random = new Random(HashCode.Combine(tenantId, now.DayOfYear));

        var existing = await _db.Prospects
            .Include(p => p.Score)
            .Where(p => p.TenantId == tenantId && p.Source == "demo_hunter")
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (existing.Count > 0)
        {
            _db.Prospects.RemoveRange(existing);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        var results = new List<HunterResultDto>(capacity: 50);
        var usedRestaurantNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedServiceNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < 50; index++)
        {
            var isRestaurant = index < 25;
            var company = isRestaurant
                ? ComposeUniqueName(random, RestaurantPrefixes, RestaurantSuffixes, usedRestaurantNames, index)
                : ComposeUniqueName(random, ServicePrefixes, ServiceSuffixes, usedServiceNames, index);
            var city = Cities[random.Next(Cities.Length)];
            var email = $"lead.demo+{Slug(company)}{index}@pipelane.app";
            var phone = GeneratePhone(random);
            var website = isRestaurant
                ? $"https://{Slug(company)}.fr"
                : $"https://{Slug(company)}.studio";

            var features = BuildFeatures(random, isRestaurant);
            var prospectDto = new HunterProspectDto(
                FirstName: SampleFirstName(random),
                LastName: SampleLastName(random),
                Company: company,
                Email: email,
                Phone: phone,
                WhatsAppMsisdn: features.SocialActive == true ? $"+33{random.Next(600000000, 699999999)}" : null,
                Website: features.HasSite == true ? website : null,
                City: city,
                Country: "France",
                Social: features.SocialActive == true
                    ? new ProspectSocialDto(
                        Instagram: $"https://www.instagram.com/{Slug(company)}",
                        LinkedIn: $"https://www.linkedin.com/company/{Slug(company)}",
                        Facebook: null)
                    : null);

            var scoreValue = ScoreService.ComputeScore(features, null);
            var why = WhyService.BuildReasons(prospectDto, features, scoreValue);

            var prospect = new Prospect
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Email = email,
                FirstName = prospectDto.FirstName,
                LastName = prospectDto.LastName,
                Company = company,
                Phone = phone,
                City = city,
                Country = "France",
                Website = features.HasSite == true ? website : null,
                Status = ProspectStatus.New,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                Industry = isRestaurant ? "Restaurants" : "PME services / Web",
                Source = "demo_hunter",
                TagsJson = "[\"demo\",\"lead_hunter\"]",
                EnrichedJson = JsonSerializer.Serialize(features, JsonOptions)
            };

            var score = new ProspectScore
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProspectId = prospect.Id,
                Score = scoreValue,
                FeaturesJson = JsonSerializer.Serialize(new { enriched = features, why }, JsonOptions),
                UpdatedAtUtc = now
            };

            await _db.Prospects.AddAsync(prospect, ct).ConfigureAwait(false);
            await _db.ProspectScores.AddAsync(score, ct).ConfigureAwait(false);

            results.Add(new HunterResultDto(
                prospect.Id,
                prospectDto,
                features,
                scoreValue,
                why));
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        _logger.LogInformation("Seeded {Count} Lead Hunter demo prospects for tenant {TenantId}", results.Count, tenantId);

        return results;
    }

    private static HunterFeaturesDto BuildFeatures(Random random, bool isRestaurant)
    {
        var rating = Math.Round(isRestaurant ? 3.2 + random.NextDouble() * 1.5 : 3.5 + random.NextDouble() * 1.2, 1);
        var reviews = isRestaurant ? random.Next(35, 520) : random.Next(12, 210);
        var hasSite = random.NextDouble() > 0.15;
        var booking = isRestaurant ? random.NextDouble() > 0.45 : random.NextDouble() > 0.7;
        var socialActive = random.NextDouble() > 0.4;
        var mobileOk = hasSite && random.NextDouble() > 0.25;
        var pixel = hasSite && random.NextDouble() > 0.6;
        var lcpSlow = hasSite && random.NextDouble() > 0.55 && !mobileOk;
        var cms = hasSite ? SampleCms(random) : null;

        return new HunterFeaturesDto(rating, reviews, hasSite, booking, socialActive, cms, mobileOk, pixel, lcpSlow);
    }

    private static string SampleFirstName(Random random) => FirstNames[random.Next(FirstNames.Length)];

    private static string SampleLastName(Random random) => LastNames[random.Next(LastNames.Length)];

    private static string SampleCms(Random random)
    {
        var cms = new[] { "WordPress", "Webflow", "Wix", "Shopify", "Squarespace" };
        return cms[random.Next(cms.Length)];
    }

    private static string ComposeUniqueName(
        Random random,
        string[] prefixes,
        string[] suffixes,
        HashSet<string> usedNames,
        int fallbackIndex)
    {
        var combinations = prefixes.Length * suffixes.Length;

        for (var attempt = 0; attempt < combinations; attempt++)
        {
            var prefix = prefixes[random.Next(prefixes.Length)];
            var suffix = suffixes[random.Next(suffixes.Length)];
            var candidate = $"{prefix} {suffix}";
            if (usedNames.Add(candidate))
            {
                return candidate;
            }
        }

        var deterministicPrefix = prefixes[fallbackIndex % prefixes.Length];
        var deterministicSuffix = suffixes[(fallbackIndex / prefixes.Length) % suffixes.Length];
        var fallback = $"{deterministicPrefix} {deterministicSuffix} {fallbackIndex + 1}";
        usedNames.Add(fallback);
        return fallback;
    }

    private static string GeneratePhone(Random random)
    {
        return $"+33 6 {random.Next(10, 99)} {random.Next(10, 99)} {random.Next(10, 99)} {random.Next(10, 99)}";
    }

    private static string Slug(string input)
    {
        var chars = input.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("é", "e").Replace("è", "e").Replace("ê", "e")
            .Replace("à", "a").Replace("â", "a")
            .Replace("î", "i").Replace("ï", "i")
            .Replace("ô", "o").Replace("ö", "o")
            .Replace("ù", "u").Replace("û", "u");
        return new string(chars.Where(ch => char.IsLetterOrDigit(ch) || ch == '-').ToArray());
    }

    private static readonly string[] FirstNames = { "Alex", "Mila", "Noah", "Léa", "Camille", "Louis", "Sacha", "Emma", "Jules", "Nora" };
    private static readonly string[] LastNames = { "Martin", "Bernard", "Lambert", "Diallo", "Girard", "Masson", "Renard", "Carre", "Fabre", "Royer" };
}

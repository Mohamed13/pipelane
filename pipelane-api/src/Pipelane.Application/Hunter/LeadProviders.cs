using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Pipelane.Application.Hunter;

namespace Pipelane.Application.Hunter;

public record LeadCandidate(HunterProspectDto Prospect, HunterFeaturesDto Features);

public interface ILeadProvider
{
    string Source { get; }
    Task<IReadOnlyList<LeadCandidate>> SearchAsync(Guid tenantId, HunterSearchCriteria criteria, CancellationToken ct);
}

public sealed class MapsStubLeadProvider : ILeadProvider
{
    private static readonly string[] DefaultIndustries = new[]
    {
        "Restaurants", "Plomberie", "Salons de coiffure", "Formations", "PME Services"
    };

    private readonly ILogger<MapsStubLeadProvider> _logger;

    public MapsStubLeadProvider(ILogger<MapsStubLeadProvider> logger)
    {
        _logger = logger;
    }

    public string Source => "mapsStub";

    public Task<IReadOnlyList<LeadCandidate>> SearchAsync(Guid tenantId, HunterSearchCriteria criteria, CancellationToken ct)
    {
        var results = new List<LeadCandidate>();
        var industry = string.IsNullOrWhiteSpace(criteria.Industry)
            ? DefaultIndustries[Math.Abs(tenantId.GetHashCode()) % DefaultIndustries.Length]
            : criteria.Industry;

        var seed = HashCode.Combine(tenantId, industry, criteria.TextQuery ?? string.Empty);
        var random = new Random(seed);
        var geoCity = criteria.Geo != null
            ? $"{ApproximateCity(criteria.Geo.Lat, criteria.Geo.Lng)}"
            : "Paris";

        for (var i = 0; i < 30; i++)
        {
            ct.ThrowIfCancellationRequested();
            var rating = Math.Round(2.8 + random.NextDouble() * 2.2, 1);
            var reviews = random.Next(5, 520);
            var hasSite = random.NextDouble() > 0.2;
            var booking = industry.Contains("restaurant", StringComparison.OrdinalIgnoreCase)
                ? random.NextDouble() > 0.4
                : random.NextDouble() > 0.6;
            var socialActive = random.NextDouble() > 0.35;
            var mobileOk = random.NextDouble() > 0.25;
            var pixel = hasSite && random.NextDouble() > 0.55;
            var lcpSlow = hasSite && random.NextDouble() > 0.6;

            var company = $"{industry.Split(' ').First()} {GenerateSuffix(random)} {i + 1:D2}";
            var features = new HunterFeaturesDto(
                rating,
                reviews,
                hasSite,
                booking,
                socialActive,
                hasSite ? GuessCms(company, random) : null,
                mobileOk,
                pixel,
                lcpSlow);

            var phone = $"+33 6 {random.Next(10, 99)} {random.Next(10, 99)} {random.Next(10, 99)} {random.Next(10, 99)}";
            var email = $"contact@{company.ToLowerInvariant().Replace(" ", string.Empty)}.fr";
            var website = hasSite ? $"https://{company.ToLowerInvariant().Replace(" ", string.Empty)}.fr" : null;

            var prospect = new HunterProspectDto(
                GenerateFirstName(random),
                GenerateLastName(random),
                company,
                email,
                phone,
                socialActive ? $"+33{random.Next(600000000, 699999999)}" : null,
                website,
                geoCity,
                "France",
                socialActive
                    ? new ProspectSocialDto(
                        $"https://www.instagram.com/{company.ToLowerInvariant().Replace(" ", string.Empty)}",
                        $"https://www.linkedin.com/company/{company.ToLowerInvariant().Replace(" ", string.Empty)}",
                        null)
                    : null);

            results.Add(new LeadCandidate(prospect, features));
        }

        return Task.FromResult<IReadOnlyList<LeadCandidate>>(results);
    }

    private static string ApproximateCity(double lat, double lng)
    {
        if (lat > 48.5 && lat < 49.2 && lng > 2 && lng < 3) return "Paris";
        if (lat > 43 && lat < 44 && lng > 5 && lng < 6) return "Marseille";
        if (lat > 45.5 && lat < 45.9 && lng > 4.6 && lng < 5) return "Lyon";
        return "France";
    }

    private static string GenerateSuffix(Random random)
    {
        var suffixes = new[] { "Plus", "Pro", "Excellence", "Nova", "Zen", "Direct" };
        return suffixes[random.Next(suffixes.Length)];
    }

    private static string GenerateFirstName(Random random)
    {
        var names = new[] { "Alex", "Camille", "Noah", "Emma", "Léa", "Louis", "Manon", "Jules" };
        return names[random.Next(names.Length)];
    }

    private static string GenerateLastName(Random random)
    {
        var names = new[] { "Martin", "Bernard", "Robert", "Petit", "Moreau", "Fournier", "Girard", "Roussel" };
        return names[random.Next(names.Length)];
    }

    private static string? GuessCms(string company, Random random)
    {
        var options = new[] { "WordPress", "Wix", "Webflow", "Shopify" };
        return random.NextDouble() > 0.3 ? options[random.Next(options.Length)] : null;
    }
}

public sealed class CsvLeadProvider : ILeadProvider
{
    private readonly IHunterCsvStore _store;
    private readonly ILogger<CsvLeadProvider> _logger;

    public CsvLeadProvider(IHunterCsvStore store, ILogger<CsvLeadProvider> logger)
    {
        _store = store;
        _logger = logger;
    }

    public string Source => "csv";

    public async Task<IReadOnlyList<LeadCandidate>> SearchAsync(Guid tenantId, HunterSearchCriteria criteria, CancellationToken ct)
    {
        if (criteria.CsvId is null)
        {
            throw new InvalidOperationException("csvId manquant pour une recherche CSV.");
        }

        await using var stream = await _store.OpenAsync(tenantId, criteria.CsvId.Value, ct);
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: false);

        var headerLine = await reader.ReadLineAsync();
        if (headerLine is null)
        {
            return Array.Empty<LeadCandidate>();
        }

        var headers = headerLine.Split(',').Select(h => h.Trim().ToLowerInvariant()).ToArray();
        var results = new List<LeadCandidate>();
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line)) continue;
            var cells = line.Split(',');
            var row = headers.Zip(cells, (h, c) => (h, c)).ToDictionary(x => x.h, x => x.c.Trim());

            var prospect = new HunterProspectDto(
                row.TryGetValue("first_name", out var first) ? first : null,
                row.TryGetValue("last_name", out var last) ? last : null,
                row.TryGetValue("company", out var company) ? company : (row.TryGetValue("organisation", out var org) ? org : null),
                row.TryGetValue("email", out var email) ? email : null,
                row.TryGetValue("phone", out var phone) ? phone : null,
                row.TryGetValue("wa_msisdn", out var wa) ? wa : null,
                row.TryGetValue("website", out var website) ? website : null,
                row.TryGetValue("city", out var city) ? city : null,
                row.TryGetValue("country", out var country) ? country : null,
                BuildSocial(row));

            var features = new HunterFeaturesDto(
                ParseNullableDouble(row, "rating"),
                ParseNullableInt(row, "reviews"),
                ParseNullableBool(row, "has_site"),
                ParseNullableBool(row, "booking"),
                ParseNullableBool(row, "social_active"),
                row.TryGetValue("cms", out var cms) ? cms : null,
                ParseNullableBool(row, "mobile_ok"),
                ParseNullableBool(row, "pixel_present"),
                ParseNullableBool(row, "lcp_slow"));

            results.Add(new LeadCandidate(prospect, features));
        }

        _logger.LogInformation("CSV lead provider returned {Count} rows for tenant {TenantId}", results.Count, tenantId);
        return results;
    }

    private static ProspectSocialDto? BuildSocial(Dictionary<string, string> row)
    {
        var instagram = row.TryGetValue("instagram", out var ig) ? ig : null;
        var linkedIn = row.TryGetValue("linkedin", out var li) ? li : null;
        var facebook = row.TryGetValue("facebook", out var fb) ? fb : null;
        if (string.IsNullOrWhiteSpace(instagram) && string.IsNullOrWhiteSpace(linkedIn) && string.IsNullOrWhiteSpace(facebook))
        {
            return null;
        }

        return new ProspectSocialDto(instagram, linkedIn, facebook);
    }

    private static double? ParseNullableDouble(Dictionary<string, string> row, string key)
    {
        return row.TryGetValue(key, out var value) && double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static int? ParseNullableInt(Dictionary<string, string> row, string key)
    {
        return row.TryGetValue(key, out var value) && int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static bool? ParseNullableBool(Dictionary<string, string> row, string key)
    {
        if (!row.TryGetValue(key, out var value)) return null;
        if (bool.TryParse(value, out var parsed)) return parsed;
        if (int.TryParse(value, out var intValue)) return intValue > 0;
        return value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("y", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class DirectoryStubLeadProvider : ILeadProvider
{
    private readonly MapsStubLeadProvider _maps;

    public DirectoryStubLeadProvider(MapsStubLeadProvider maps)
    {
        _maps = maps;
    }

    public string Source => "directoryStub";

    public Task<IReadOnlyList<LeadCandidate>> SearchAsync(Guid tenantId, HunterSearchCriteria criteria, CancellationToken ct)
    {
        // For now reuse maps stub
        return _maps.SearchAsync(tenantId, criteria, ct);
    }
}

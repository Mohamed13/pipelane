using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

namespace Pipelane.Application.Hunter;

public interface IHunterEnrichService
{
    Task<HunterFeaturesDto> EnrichAsync(HunterProspectDto prospect, HunterFilters? filters, HunterFeaturesDto candidate, CancellationToken ct);
}

public sealed class HunterEnrichService : IHunterEnrichService
{
    private readonly ILogger<HunterEnrichService> _logger;

    public HunterEnrichService(ILogger<HunterEnrichService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<HunterFeaturesDto> EnrichAsync(HunterProspectDto prospect, HunterFilters? filters, HunterFeaturesDto candidate, CancellationToken ct)
    {
        var hasSite = candidate.HasSite ?? !string.IsNullOrWhiteSpace(prospect.Website);
        var mobileOk = candidate.MobileOk ?? true;
        var pixelPresent = candidate.PixelPresent ?? false;
        var cms = candidate.Cms;
        var lcpSlow = candidate.LcpSlow;

        if (hasSite && !string.IsNullOrWhiteSpace(prospect.Website))
        {
            try
            {
                await Task.Delay(10, ct);
                hasSite = prospect.Website!.StartsWith("http", StringComparison.OrdinalIgnoreCase);
                if (string.IsNullOrWhiteSpace(cms))
                {
                    cms = prospect.Website.Contains("shop", StringComparison.OrdinalIgnoreCase)
                        ? "Shopify"
                        : prospect.Website.Contains("webflow", StringComparison.OrdinalIgnoreCase)
                            ? "Webflow"
                            : "WordPress";
                }
                pixelPresent = candidate.PixelPresent ?? prospect.Website.Contains("meta", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "HEAD request failed for {Website}", prospect.Website);
                hasSite = false;
            }
        }

        if (filters?.HasSite == true)
        {
            hasSite = true;
        }

        if (filters?.SocialActive == true && candidate.SocialActive is null)
        {
            var socialActive = prospect.Social is not null &&
                (!string.IsNullOrWhiteSpace(prospect.Social.Instagram) || !string.IsNullOrWhiteSpace(prospect.Social.LinkedIn));
            candidate = candidate with { SocialActive = socialActive };
        }

        return candidate with
        {
            HasSite = hasSite,
            MobileOk = mobileOk,
            PixelPresent = pixelPresent,
            Cms = cms,
            LcpSlow = lcpSlow ?? false
        };
    }
}

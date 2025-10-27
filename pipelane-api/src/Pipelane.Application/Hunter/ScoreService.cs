using System;

namespace Pipelane.Application.Hunter;

public interface IHunterScoreService
{
    int ComputeScore(HunterFeaturesDto features, HunterFilters? filters);
}

public sealed class HunterScoreService : IHunterScoreService
{
    /// <inheritdoc/>
    public int ComputeScore(HunterFeaturesDto features, HunterFilters? filters)
    {
        var ratingNorm = NormalizeRating(features.Rating);
        var reviewsNorm = NormalizeLog(features.Reviews);
        var hasSite = BoolToScore(features.HasSite);
        var social = BoolToScore(features.SocialActive);
        var booking = BoolToScore(features.Booking);
        var mobile = BoolToScore(features.MobileOk);
        var priceBand = PriceBandScore(filters?.PriceBand);
        var geoScore = 0.5; // placeholder until geo heatmap implemented
        var sizeBin = SizeFromReviews(features.Reviews);
        var lcpInverse = features.LcpSlow == true ? 0.2 : 0.9;

        var weighted =
            0.18 * ratingNorm +
            0.10 * reviewsNorm +
            0.12 * hasSite +
            0.12 * social +
            0.12 * booking +
            0.10 * priceBand +
            0.10 * geoScore +
            0.08 * sizeBin +
            0.08 * lcpInverse;

        var clamped = Clamp(weighted, 0, 1);
        return (int)Math.Round(100 * clamped, MidpointRounding.AwayFromZero);
    }

    private static double NormalizeRating(double? rating)
    {
        if (rating is null) return 0.5;
        return Clamp((double)rating / 5d, 0, 1);
    }

    private static double NormalizeLog(int? reviews)
    {
        if (reviews is null || reviews <= 0) return 0.5;
        var log = Math.Log10(reviews.Value + 1);
        return Clamp(log / 3, 0, 1);
    }

    private static double BoolToScore(bool? value)
    {
        if (value is null) return 0.5;
        return value.Value ? 1 : 0.2;
    }

    private static double PriceBandScore(string? priceBand)
    {
        if (string.IsNullOrWhiteSpace(priceBand)) return 0.5;
        return priceBand.ToLowerInvariant() switch
        {
            "low" or "€" => 0.6,
            "medium" or "€€" => 0.8,
            "high" or "€€€" => 0.4,
            _ => 0.5
        };
    }

    private static double SizeFromReviews(int? reviews)
    {
        if (reviews is null) return 0.5;
        if (reviews < 20) return 0.4;
        if (reviews < 100) return 0.6;
        if (reviews < 300) return 0.8;
        return 0.9;
    }

    private static double Clamp(double value, double min, double max)
        => Math.Max(min, Math.Min(max, value));
}

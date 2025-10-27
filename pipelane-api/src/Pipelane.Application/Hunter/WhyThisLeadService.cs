using System;
using System.Collections.Generic;

namespace Pipelane.Application.Hunter;

public interface IWhyThisLeadService
{
    IReadOnlyList<string> BuildReasons(HunterProspectDto prospect, HunterFeaturesDto features, int score);
}

public sealed class WhyThisLeadService : IWhyThisLeadService
{
    /// <inheritdoc/>
    public IReadOnlyList<string> BuildReasons(HunterProspectDto prospect, HunterFeaturesDto features, int score)
    {
        var reasons = new List<string>();

        if (features.Booking == false)
        {
            reasons.Add("Pas de réservation en ligne");
        }
        if (features.MobileOk == false)
        {
            reasons.Add("Site lent ou peu lisible sur mobile");
        }
        if (features.SocialActive == true)
        {
            reasons.Add("Réseaux sociaux actifs");
        }
        if (features.SocialActive == false)
        {
            reasons.Add("Présence sociale faible : opportunité d'accompagnement");
        }
        if (features.HasSite == false)
        {
            reasons.Add("Site web à créer ou optimiser");
        }
        if (features.Rating is { } rating)
        {
            if (rating >= 4.5) reasons.Add("Notes élevées, prospects chauds");
            else if (rating < 3.2) reasons.Add("Avis perfectibles : opportunité de réassurance");
        }
        if (prospect.City is not null)
        {
            reasons.Add($"Localisé à {prospect.City}");
        }

        if (reasons.Count == 0)
        {
            reasons.Add("Profil cohérent avec vos critères");
        }

        return reasons.Count <= 3 ? reasons : reasons.GetRange(0, 3);
    }
}

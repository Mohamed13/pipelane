using Microsoft.AspNetCore.Http;

namespace Pipelane.Api.Middleware;

/// <summary>
/// Fournit des libellés français cohérents pour les réponses <see cref="Microsoft.AspNetCore.Mvc.ProblemDetails"/>.
/// </summary>
internal static class ProblemDetailsFrenchLocalizer
{
    /// <summary>
    /// Retourne un titre lisible en fonction du code HTTP.
    /// </summary>
    /// <param name="statusCode">Code de statut HTTP.</param>
    /// <returns>Titre localisé.</returns>
    public static string ResolveTitle(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "Requête invalide",
        StatusCodes.Status401Unauthorized => "Authentification requise",
        StatusCodes.Status403Forbidden => "Accès refusé",
        StatusCodes.Status404NotFound => "Ressource introuvable",
        StatusCodes.Status409Conflict => "Conflit métier",
        StatusCodes.Status422UnprocessableEntity => "Données incohérentes",
        StatusCodes.Status429TooManyRequests => "Quota dépassé",
        StatusCodes.Status503ServiceUnavailable => "Service temporairement indisponible",
        _ => "Erreur interne"
    };

    /// <summary>
    /// Retourne un détail par défaut cohérent en français.
    /// </summary>
    /// <param name="statusCode">Code de statut HTTP.</param>
    /// <returns>Détail localisé.</returns>
    public static string ResolveDetail(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "La requête contient des paramètres manquants ou invalides.",
        StatusCodes.Status401Unauthorized => "Vous devez être authentifié pour accéder à cette ressource.",
        StatusCodes.Status403Forbidden => "Vous n'êtes pas autorisé à accéder à cette ressource.",
        StatusCodes.Status404NotFound => "La ressource demandée est introuvable ou a été supprimée.",
        StatusCodes.Status409Conflict => "L'opération est en conflit avec l'état actuel des données.",
        StatusCodes.Status422UnprocessableEntity => "Les données envoyées ne respectent pas les contraintes métier.",
        StatusCodes.Status429TooManyRequests => "Trop de requêtes ont été envoyées dans un laps de temps restreint.",
        StatusCodes.Status503ServiceUnavailable => "Le service est temporairement indisponible. Merci de réessayer plus tard.",
        _ => "Une erreur interne est survenue. Notre équipe a été notifiée."
    };
}

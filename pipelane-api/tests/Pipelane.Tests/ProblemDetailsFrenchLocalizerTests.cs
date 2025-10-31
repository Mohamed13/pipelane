using Microsoft.AspNetCore.Http;

using Pipelane.Api.Middleware;

using Xunit;

namespace Pipelane.Tests;

public class ProblemDetailsFrenchLocalizerTests
{
    [Theory]
    [InlineData(StatusCodes.Status400BadRequest, "Requête invalide")]
    [InlineData(StatusCodes.Status401Unauthorized, "Authentification requise")]
    [InlineData(StatusCodes.Status403Forbidden, "Accès refusé")]
    [InlineData(StatusCodes.Status404NotFound, "Ressource introuvable")]
    [InlineData(StatusCodes.Status409Conflict, "Conflit métier")]
    [InlineData(StatusCodes.Status422UnprocessableEntity, "Données incohérentes")]
    [InlineData(StatusCodes.Status429TooManyRequests, "Quota dépassé")]
    [InlineData(StatusCodes.Status503ServiceUnavailable, "Service temporairement indisponible")]
    public void ResolveTitle_returns_expected_translation(int status, string expected)
    {
        var title = ProblemDetailsFrenchLocalizer.ResolveTitle(status);

        Assert.Equal(expected, title);
    }

    [Fact]
    public void ResolveTitle_defaults_to_internal_error_for_unknown_code()
    {
        var title = ProblemDetailsFrenchLocalizer.ResolveTitle(599);
        Assert.Equal("Erreur interne", title);
    }

    [Theory]
    [InlineData(StatusCodes.Status400BadRequest, "La requête contient des paramètres manquants ou invalides.")]
    [InlineData(StatusCodes.Status401Unauthorized, "Vous devez être authentifié pour accéder à cette ressource.")]
    [InlineData(StatusCodes.Status403Forbidden, "Vous n'êtes pas autorisé à accéder à cette ressource.")]
    [InlineData(StatusCodes.Status404NotFound, "La ressource demandée est introuvable ou a été supprimée.")]
    [InlineData(StatusCodes.Status409Conflict, "L'opération est en conflit avec l'état actuel des données.")]
    [InlineData(StatusCodes.Status422UnprocessableEntity, "Les données envoyées ne respectent pas les contraintes métier.")]
    [InlineData(StatusCodes.Status429TooManyRequests, "Trop de requêtes ont été envoyées dans un laps de temps restreint.")]
    [InlineData(StatusCodes.Status503ServiceUnavailable, "Le service est temporairement indisponible. Merci de réessayer plus tard.")]
    public void ResolveDetail_returns_expected_translation(int status, string expected)
    {
        var detail = ProblemDetailsFrenchLocalizer.ResolveDetail(status);

        Assert.Equal(expected, detail);
    }

    [Fact]
    public void ResolveDetail_defaults_to_internal_message_for_unknown_code()
    {
        var detail = ProblemDetailsFrenchLocalizer.ResolveDetail(599);
        Assert.Equal("Une erreur interne est survenue. Notre équipe a été notifiée.", detail);
    }
}

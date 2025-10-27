using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Pipelane.Application.Common;

/// <summary>
/// Utilitaires de validation rapides pour protéger les invariants des services applicatifs.
/// </summary>
public static class Guard
{
    /// <summary>
    /// Vérifie qu'une référence n'est pas nulle et la retourne.
    /// </summary>
    /// <typeparam name="T">Type de la référence.</typeparam>
    /// <param name="value">Valeur à valider.</param>
    /// <param name="parameterName">Nom du paramètre.</param>
    /// <returns>La valeur validée.</returns>
    /// <exception cref="ArgumentNullException">Jetée si la valeur est nulle.</exception>
    public static T NotNull<T>([NotNull] T? value, string parameterName)
        where T : class
    {
        if (value is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        return value;
    }

    /// <summary>
    /// Vérifie qu'une chaîne n'est pas vide ou composée uniquement d'espaces et la retourne nettoyée.
    /// </summary>
    /// <param name="value">Valeur à valider.</param>
    /// <param name="parameterName">Nom du paramètre.</param>
    /// <returns>Chaîne épurée.</returns>
    /// <exception cref="ArgumentException">Jetée si la valeur est vide.</exception>
    public static string NotNullOrWhiteSpace(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"Le paramètre '{parameterName}' ne peut pas être vide.", parameterName);
        }

        return value.Trim();
    }

    /// <summary>
    /// Vérifie qu'un Guid n'est pas vide.
    /// </summary>
    /// <param name="value">Guid à valider.</param>
    /// <param name="parameterName">Nom du paramètre.</param>
    /// <returns>Guid validé.</returns>
    /// <exception cref="ArgumentException">Jetée si le Guid est vide.</exception>
    public static Guid NotEmpty(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException($"Le paramètre '{parameterName}' doit être un Guid valide.", parameterName);
        }

        return value;
    }

    /// <summary>
    /// Vérifie qu'un type de valeur n'est pas égal à défaut.
    /// </summary>
    /// <typeparam name="T">Type de valeur.</typeparam>
    /// <param name="value">Valeur à valider.</param>
    /// <param name="parameterName">Nom du paramètre.</param>
    /// <returns>Valeur validée.</returns>
    /// <exception cref="ArgumentException">Jetée si la valeur est égale à défaut.</exception>
    public static T NotDefault<T>(T value, string parameterName)
        where T : struct
    {
        if (EqualityComparer<T>.Default.Equals(value, default))
        {
            throw new ArgumentException($"Le paramètre '{parameterName}' doit être initialisé.", parameterName);
        }

        return value;
    }

    /// <summary>
    /// Vérifie qu'une collection n'est pas nulle ni vide et la retourne.
    /// </summary>
    /// <typeparam name="T">Type des éléments.</typeparam>
    /// <param name="items">Collection à valider.</param>
    /// <param name="parameterName">Nom du paramètre.</param>
    /// <returns>Collection validée.</returns>
    /// <exception cref="ArgumentException">Jetée si la collection est nulle ou vide.</exception>
    public static IReadOnlyCollection<T> NotNullOrEmpty<T>(IReadOnlyCollection<T>? items, string parameterName)
    {
        if (items is null || items.Count == 0)
        {
            throw new ArgumentException($"Le paramètre '{parameterName}' doit contenir au moins un élément.", parameterName);
        }

        return items;
    }
}

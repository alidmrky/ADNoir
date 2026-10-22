using System.Globalization;

namespace AdNoir.BuildingBlocks.Messages;

/// <summary>
/// Turns a message code plus arguments into text for the caller's culture.
/// </summary>
/// <remarks>
/// Service code never calls this. Resolution happens centrally when the response is written,
/// in the same place correlation and trace identifiers are filled in, so that a service
/// method never produces user-facing text. The client receives both the resolved text and
/// the code with its arguments, so a client that prefers to translate for itself - a mobile
/// app, typically - can do so.
/// </remarks>
public interface IMessageResolver
{
    /// <summary>Resolves a code for a culture, falling back to the default culture.</summary>
    /// <param name="code">The message code.</param>
    /// <param name="args">Named placeholder values, or null.</param>
    /// <param name="culture">The culture to resolve for, or null to use the ambient culture.</param>
    /// <returns>
    /// The resolved text. When the code has no entry in any culture the code itself is
    /// returned, with its arguments appended, so the gap is visible rather than blank.
    /// </returns>
    string Resolve(string code, IReadOnlyDictionary<string, object?>? args = null, CultureInfo? culture = null);

    /// <summary>Whether a code can be resolved in the given culture, without falling back.</summary>
    /// <param name="code">The message code.</param>
    /// <param name="culture">The culture name.</param>
    /// <returns><see langword="true"/> when the culture has an entry for the code.</returns>
    bool CanResolve(string code, string culture);
}

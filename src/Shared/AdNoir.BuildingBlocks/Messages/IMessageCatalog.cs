namespace AdNoir.BuildingBlocks.Messages;

/// <summary>
/// The loaded message catalog: every supported culture's code-to-text map.
/// </summary>
/// <remarks>
/// <para>
/// The catalog lives in JSON files in the repository, not in the database. Files are
/// versioned in git, their diffs are reviewable, they ship with the code and they can be
/// tested. A database-backed catalog looks more flexible but becomes an unversioned mess;
/// if per-tenant customisation is needed later, the database is added as an
/// <em>override layer only</em>, with the file remaining the default.
/// </para>
/// <para>
/// This interface exposes the raw entries because the integrity test needs them: it checks
/// that every <c>MessageCodes</c> constant has an entry in every culture, that no entry is
/// orphaned, and that cultures agree on placeholder names.
/// </para>
/// </remarks>
public interface IMessageCatalog
{
    /// <summary>Cultures the catalog was loaded for, e.g. <c>en</c> and <c>tr</c>.</summary>
    IReadOnlyList<string> SupportedCultures { get; }

    /// <summary>The culture used when the requested one has no entry.</summary>
    string DefaultCulture { get; }

    /// <summary>All entries for a culture.</summary>
    /// <param name="culture">The culture name.</param>
    /// <returns>The code-to-text map, or an empty map when the culture is not loaded.</returns>
    IReadOnlyDictionary<string, string> Entries(string culture);

    /// <summary>Looks up a single entry without falling back to another culture.</summary>
    /// <param name="culture">The culture name.</param>
    /// <param name="code">The message code.</param>
    /// <param name="text">The text, when found.</param>
    /// <returns><see langword="true"/> when the culture has an entry for the code.</returns>
    bool TryGet(string culture, string code, out string? text);
}

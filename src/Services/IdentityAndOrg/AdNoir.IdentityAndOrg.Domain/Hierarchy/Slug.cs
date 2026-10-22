using System.Globalization;
using System.Text;

namespace AdNoir.IdentityAndOrg.Domain.Hierarchy;

/// <summary>
/// Produces the URL-safe, human-readable segments that make up a hierarchy path.
/// </summary>
/// <remarks>
/// <para>
/// A <c>HierarchyPath</c> is built from human-readable slugs, and how one is produced matters
/// because a slug is persisted data: changing the rules later invalidates every stored path.
/// </para>
/// <para>
/// A slug is stored independently of the name it was derived from. Renaming a unit and
/// re-slugging it are separate decisions - the second one rewrites the whole subtree, so it
/// should not happen by accident every time someone fixes a typo in a display name.
/// </para>
/// </remarks>
public static class Slug
{
    /// <summary>Maximum length of a single slug segment.</summary>
    public const int MaxLength = 64;

    private static readonly (char From, string To)[] TurkishReplacements =
    [
        ('ı', "i"), ('İ', "i"), ('ş', "s"), ('Ş', "s"), ('ğ', "g"), ('Ğ', "g"),
        ('ü', "u"), ('Ü', "u"), ('ö', "o"), ('Ö', "o"), ('ç', "c"), ('Ç', "c"),
    ];

    /// <summary>Derives a slug from arbitrary text.</summary>
    /// <param name="value">The source text, typically a unit name.</param>
    /// <returns>The slug, or an empty string when nothing usable remains.</returns>
    /// <remarks>
    /// Turkish characters are mapped explicitly before Unicode normalisation, because
    /// stripping diacritics alone turns dotless <c>ı</c> into an empty result and <c>İ</c>
    /// into <c>i̇</c> with a combining dot. Handling them by hand keeps
    /// <c>Çankaya Şubesi</c> readable as <c>cankaya-subesi</c> rather than mangled.
    /// </remarks>
    public static string From(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        StringBuilder mapped = new(value.Length);

        foreach (char character in value)
        {
            string? replacement = null;

            foreach ((char from, string to) in TurkishReplacements)
            {
                if (character == from)
                {
                    replacement = to;
                    break;
                }
            }

            mapped.Append(replacement ?? character.ToString());
        }

        string normalized = mapped.ToString().Normalize(NormalizationForm.FormD);

        StringBuilder builder = new(normalized.Length);
        bool lastWasSeparator = false;

        foreach (char character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                lastWasSeparator = false;
                continue;
            }

            // Collapse any run of non-alphanumeric characters into a single separator.
            if (!lastWasSeparator && builder.Length > 0)
            {
                builder.Append('-');
                lastWasSeparator = true;
            }
        }

        string slug = builder.ToString().Trim('-');

        if (slug.Length > MaxLength)
        {
            slug = slug[..MaxLength].TrimEnd('-');
        }

        return slug;
    }

    /// <summary>Whether a string is already a well-formed slug.</summary>
    /// <param name="slug">The candidate.</param>
    /// <returns><see langword="true"/> when it is lower-case alphanumeric with single dashes.</returns>
    public static bool IsValid(string? slug)
    {
        if (string.IsNullOrEmpty(slug) || slug.Length > MaxLength)
        {
            return false;
        }

        if (slug[0] == '-' || slug[^1] == '-')
        {
            return false;
        }

        char previous = '\0';

        foreach (char character in slug)
        {
            bool allowed = char.IsAsciiDigit(character) ||
                           (char.IsAsciiLetter(character) && char.IsLower(character)) ||
                           character == '-';

            if (!allowed || (character == '-' && previous == '-'))
            {
                return false;
            }

            previous = character;
        }

        return true;
    }

    /// <summary>
    /// Appends a numeric suffix to resolve a collision, keeping the result within
    /// <see cref="MaxLength"/>.
    /// </summary>
    /// <param name="slug">The colliding slug.</param>
    /// <param name="attempt">The attempt number; 2 produces <c>-2</c>.</param>
    /// <returns>The suffixed slug.</returns>
    public static string WithSuffix(string slug, int attempt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentOutOfRangeException.ThrowIfLessThan(attempt, 2);

        string suffix = "-" + attempt.ToString(CultureInfo.InvariantCulture);
        int available = MaxLength - suffix.Length;

        string head = slug.Length > available ? slug[..available].TrimEnd('-') : slug;

        return head + suffix;
    }
}

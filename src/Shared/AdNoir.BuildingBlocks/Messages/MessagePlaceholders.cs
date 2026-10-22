using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AdNoir.BuildingBlocks.Messages;

/// <summary>
/// Reads and fills the named placeholders in catalog entries.
/// </summary>
/// <remarks>
/// Placeholders are named - <c>{userId}</c>, <c>{unitName}</c> - never positional. A
/// positional catalog breaks silently when a translator reorders a sentence, which is
/// exactly what translators do.
/// </remarks>
public static partial class MessagePlaceholders
{
    [GeneratedRegex(@"\{(?<name>[A-Za-z_][A-Za-z0-9_]*)\}", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderPattern();

    /// <summary>Extracts the distinct placeholder names used by a template.</summary>
    /// <param name="template">The catalog entry text.</param>
    /// <returns>The placeholder names, ordered, without duplicates.</returns>
    public static IReadOnlyList<string> Extract(string template)
    {
        if (string.IsNullOrEmpty(template))
        {
            return [];
        }

        SortedSet<string> names = new(StringComparer.Ordinal);

        foreach (Match match in PlaceholderPattern().Matches(template))
        {
            names.Add(match.Groups["name"].Value);
        }

        return [.. names];
    }

    /// <summary>
    /// Substitutes argument values into a template.
    /// </summary>
    /// <param name="template">The catalog entry text.</param>
    /// <param name="args">Named values, or null.</param>
    /// <returns>The filled text.</returns>
    /// <remarks>
    /// A placeholder with no matching argument is left as-is rather than blanked. Leaving
    /// <c>{userId}</c> visible makes the gap obvious in a log or a screenshot; substituting
    /// an empty string produces a sentence that reads as if nothing were wrong.
    /// </remarks>
    public static string Format(string template, IReadOnlyDictionary<string, object?>? args)
    {
        if (string.IsNullOrEmpty(template) || args is null || args.Count == 0)
        {
            return template;
        }

        return PlaceholderPattern().Replace(
            template,
            match =>
            {
                string name = match.Groups["name"].Value;

                return args.TryGetValue(name, out object? value)
                    ? Stringify(value)
                    : match.Value;
            });
    }

    /// <summary>
    /// Renders a code and its arguments as a diagnostic string, used when a code has no
    /// catalog entry at all.
    /// </summary>
    /// <param name="code">The unresolved message code.</param>
    /// <param name="args">Named values, or null.</param>
    /// <returns>A string that makes the missing entry obvious rather than hiding it.</returns>
    public static string Describe(string code, IReadOnlyDictionary<string, object?>? args)
    {
        if (args is null || args.Count == 0)
        {
            return code;
        }

        StringBuilder builder = new(code);
        builder.Append(" (");

        bool first = true;
        foreach ((string key, object? value) in args)
        {
            if (!first)
            {
                builder.Append(", ");
            }

            builder.Append(key).Append('=').Append(Stringify(value));
            first = false;
        }

        builder.Append(')');
        return builder.ToString();
    }

    private static string Stringify(object? value) =>
        value switch
        {
            null => string.Empty,
            IFormattable formattable => formattable.ToString(format: null, formatProvider: CultureInfo.CurrentCulture),
            _ => value.ToString() ?? string.Empty,
        };
}

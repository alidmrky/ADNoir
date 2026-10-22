using System.Globalization;

namespace AdNoir.BuildingBlocks.Messages;

/// <summary>
/// Default <see cref="IMessageResolver"/>: exact culture, then parent culture, then the
/// configured default.
/// </summary>
/// <remarks>
/// The parent-culture step matters in practice. <c>Accept-Language: tr-TR</c> is far more
/// common than <c>tr</c>, and without the fallback every Turkish caller would silently get
/// English text while the catalog looked complete.
/// </remarks>
public sealed class MessageResolver(IMessageCatalog catalog) : IMessageResolver
{
    private readonly IMessageCatalog _catalog = catalog;

    /// <inheritdoc />
    public string Resolve(
        string code,
        IReadOnlyDictionary<string, object?>? args = null,
        CultureInfo? culture = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        CultureInfo effective = culture ?? CultureInfo.CurrentUICulture;

        return TryResolveTemplate(code, effective, out string? template)
            ? MessagePlaceholders.Format(template!, args)
            : MessagePlaceholders.Describe(code, args);
    }

    /// <inheritdoc />
    public bool CanResolve(string code, string culture) => _catalog.TryGet(culture, code, out _);

    private bool TryResolveTemplate(string code, CultureInfo culture, out string? template)
    {
        // Exact culture, e.g. "tr-TR".
        if (_catalog.TryGet(culture.Name, code, out template))
        {
            return true;
        }

        // Parent culture, e.g. "tr" for "tr-TR".
        CultureInfo parent = culture.Parent;
        while (!string.IsNullOrEmpty(parent.Name))
        {
            if (_catalog.TryGet(parent.Name, code, out template))
            {
                return true;
            }

            parent = parent.Parent;
        }

        // Configured default.
        return _catalog.TryGet(_catalog.DefaultCulture, code, out template);
    }
}

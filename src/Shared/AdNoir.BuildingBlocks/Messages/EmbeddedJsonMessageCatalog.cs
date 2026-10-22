using System.Collections.Frozen;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace AdNoir.BuildingBlocks.Messages;

/// <summary>
/// Loads the message catalog from the JSON resources embedded in this assembly
/// (<c>Messages/Resources/messages.{culture}.json</c>).
/// </summary>
/// <remarks>
/// Loaded once at construction and registered as a singleton: the catalog does not change at
/// runtime, and reading a file per request to resolve an error message would be an odd place
/// to spend I/O. The maps are frozen because they are read on every response and never written.
/// </remarks>
public sealed class EmbeddedJsonMessageCatalog : IMessageCatalog
{
    private const string ResourcePrefix = "AdNoir.BuildingBlocks.Messages.Resources.messages.";
    private const string ResourceSuffix = ".json";

    private static readonly FrozenDictionary<string, string> EmptyEntries =
        FrozenDictionary<string, string>.Empty;

    private readonly FrozenDictionary<string, FrozenDictionary<string, string>> _byCulture;

    /// <summary>Loads the catalog for the configured cultures.</summary>
    /// <param name="options">Culture settings.</param>
    /// <exception cref="InvalidOperationException">
    /// A configured culture has no embedded resource, or a resource is not valid JSON. This
    /// fails at startup on purpose: a missing catalog file would otherwise show up as raw
    /// keys on a user's screen, which is normally noticed only in production.
    /// </exception>
    public EmbeddedJsonMessageCatalog(IOptions<LocalizationOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        LocalizationOptions settings = options.Value;
        DefaultCulture = settings.DefaultCulture;
        SupportedCultures = [.. settings.SupportedCultures];

        Dictionary<string, FrozenDictionary<string, string>> loaded = new(StringComparer.OrdinalIgnoreCase);

        foreach (string culture in SupportedCultures)
        {
            loaded[culture] = Load(culture);
        }

        _byCulture = loaded.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

        if (!_byCulture.ContainsKey(DefaultCulture))
        {
            throw new InvalidOperationException(
                $"The default culture '{DefaultCulture}' is not among the supported cultures " +
                $"({string.Join(", ", SupportedCultures)}), so unresolved codes would have nowhere to fall back to.");
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<string> SupportedCultures { get; }

    /// <inheritdoc />
    public string DefaultCulture { get; }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> Entries(string culture) =>
        _byCulture.TryGetValue(culture, out FrozenDictionary<string, string>? entries) ? entries : EmptyEntries;

    /// <inheritdoc />
    public bool TryGet(string culture, string code, out string? text)
    {
        if (_byCulture.TryGetValue(culture, out FrozenDictionary<string, string>? entries) &&
            entries.TryGetValue(code, out string? value))
        {
            text = value;
            return true;
        }

        text = null;
        return false;
    }

    private static FrozenDictionary<string, string> Load(string culture)
    {
        string resourceName = string.Create(
            CultureInfo.InvariantCulture,
            $"{ResourcePrefix}{culture}{ResourceSuffix}");

        Assembly assembly = typeof(EmbeddedJsonMessageCatalog).Assembly;

        using Stream? stream = assembly.GetManifestResourceStream(resourceName);

        if (stream is null)
        {
            throw new InvalidOperationException(
                $"Message catalog resource '{resourceName}' was not found. Every supported culture " +
                $"must have a file at Messages/Resources/messages.{culture}.json included as an EmbeddedResource.");
        }

        Dictionary<string, string>? entries;

        try
        {
            entries = JsonSerializer.Deserialize<Dictionary<string, string>>(stream, MessageCatalogJson.Options);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"Message catalog resource '{resourceName}' is not valid JSON.", exception);
        }

        return entries is null
            ? FrozenDictionary<string, string>.Empty
            : entries.ToFrozenDictionary(StringComparer.Ordinal);
    }
}

/// <summary>Serializer settings used to read catalog files.</summary>
internal static class MessageCatalogJson
{
    /// <summary>Catalog files are plain string maps; comments are allowed for translator notes.</summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };
}

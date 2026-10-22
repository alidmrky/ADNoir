namespace AdNoir.BuildingBlocks.Messages;

/// <summary>
/// Culture settings for the message catalog, bound from <c>AdNoir:Localization</c>.
/// </summary>
public sealed class LocalizationOptions
{
    /// <summary>The configuration section these options bind to.</summary>
    public const string SectionName = "AdNoir:Localization";

    /// <summary>
    /// Culture used when the caller's culture has no entry for a code, or when
    /// <c>Accept-Language</c> names a culture the catalog does not carry.
    /// </summary>
    public string DefaultCulture { get; set; } = "en";

    /// <summary>Cultures the catalog is loaded for: <c>en</c> and <c>tr</c>.</summary>
    public IList<string> SupportedCultures { get; set; } = ["en", "tr"];
}

using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AdNoir.BuildingBlocks.Messages;

/// <summary>Request culture selection for the message catalog.</summary>
public static class LocalizationApplicationBuilderExtensions
{
    /// <summary>
    /// Sets the request culture from <c>Accept-Language</c>, restricted to the cultures the
    /// catalog was loaded for.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The same application builder.</returns>
    /// <remarks>
    /// <para>
    /// Without this the catalog resolves against the server's own culture and every caller
    /// gets the same language regardless of what they asked for. Nothing fails, the response
    /// is simply always in one language - which is easy to miss on a machine whose default
    /// culture happens to be the one you expected to see.
    /// </para>
    /// <para>
    /// Must run before anything that writes a response, since the resolver reads
    /// <see cref="CultureInfo.CurrentUICulture"/> at write time.
    /// </para>
    /// </remarks>
    public static IApplicationBuilder UseAdNoirLocalization(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        LocalizationOptions settings = app.ApplicationServices
            .GetRequiredService<IOptions<LocalizationOptions>>()
            .Value;

        List<CultureInfo> supported = [.. settings.SupportedCultures.Select(CultureInfo.GetCultureInfo)];

        RequestLocalizationOptions options = new()
        {
            DefaultRequestCulture = new RequestCulture(settings.DefaultCulture),
            SupportedCultures = supported,
            SupportedUICultures = supported,

            // "tr-TR" resolves to the "tr" catalog. Regional variants are far more common in
            // Accept-Language than bare language tags, so without this most requests would
            // silently fall back to the default culture.
            ApplyCurrentCultureToResponseHeaders = true,
        };

        return app.UseRequestLocalization(options);
    }
}

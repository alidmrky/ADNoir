using System.Text.Json;
using AdNoir.Notification.Infrastructure.Configuration;

namespace AdNoir.Notification.Infrastructure.Templates;

/// <summary>Loads trusted embedded catalogs or tenant configuration and pins rendered content.</summary>
public static class WelcomeTemplates
{
    /// <summary>Selects a supported language and validates the configured template.</summary>
    public static WelcomeTemplate Resolve(TenantEmailOptions? tenant, string culture)
    {
        WelcomeTemplate template;
        if (tenant?.Templates.TryGetValue(culture, out WelcomeTemplate? custom) == true)
        {
            template = custom;
        }
        else
        {
            using Stream stream = typeof(WelcomeTemplates).Assembly.GetManifestResourceStream(
                $"AdNoir.Notification.Infrastructure.Templates.welcome.{culture}.json")
                ?? throw new InvalidOperationException("Welcome template resource is missing.");
            template = JsonSerializer.Deserialize<WelcomeTemplate>(stream, JsonSerializerOptions.Web)
                ?? throw new InvalidOperationException("Welcome template resource is invalid.");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(template.Version);
        ArgumentException.ThrowIfNullOrWhiteSpace(template.Subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(template.Body);
        if (template.Version.Length > 64 || template.Subject.Length > 200 || template.Body.Length > 16000 ||
            template.Subject.Contains('\r', StringComparison.Ordinal) || template.Subject.Contains('\n', StringComparison.Ordinal) ||
            template.Subject.Contains('{', StringComparison.Ordinal) ||
            template.Body.Replace("{displayName}", string.Empty, StringComparison.Ordinal).Contains('{', StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Welcome template bounds or placeholders are invalid.");
        }
        return template;
    }
}

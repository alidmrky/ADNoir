using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace AdNoir.BuildingBlocks.Configuration;

/// <summary>Registration helpers for the <c>.env</c> configuration source.</summary>
public static class DotEnvConfigurationExtensions
{
    /// <summary>Adds a <c>.env</c> file to the configuration pipeline.</summary>
    /// <param name="builder">The configuration builder.</param>
    /// <param name="path">Path to the file.</param>
    /// <param name="optional">Whether a missing file is acceptable.</param>
    /// <returns>The same builder.</returns>
    public static IConfigurationBuilder AddDotEnvFile(
        this IConfigurationBuilder builder,
        string path = ".env",
        bool optional = true)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return builder.Add(new DotEnvConfigurationSource { Path = path, Optional = optional });
    }

    /// <summary>
    /// Adds the <c>.env</c> file outside production only.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <param name="environment">The host environment.</param>
    /// <param name="path">Path to the file.</param>
    /// <returns>The same builder.</returns>
    /// <remarks>
    /// In Production the settings must come from real environment variables. A file dropped
    /// next to the binary should never be able to override them - that is how a development
    /// connection string ends up serving live traffic.
    /// </remarks>
    public static IConfigurationBuilder AddDotEnvFileForDevelopment(
        this IConfigurationBuilder builder,
        IHostEnvironment environment,
        string path = ".env")
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(environment);

        return environment.IsProduction()
            ? builder
            : builder.AddDotEnvFile(path, optional: true);
    }
}

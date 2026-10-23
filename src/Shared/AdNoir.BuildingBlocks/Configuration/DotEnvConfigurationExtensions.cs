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
    /// Adds the <c>.env</c> file in Development only, resolving the default file from the content root.
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

        if (!environment.IsDevelopment())
        {
            return builder;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return builder.AddDotEnvFile(path == ".env" ? FindDevelopmentFile(environment.ContentRootPath) : path, optional: true);
    }

    private static string FindDevelopmentFile(string contentRoot)
    {
        string localPath = Path.Combine(contentRoot, ".env");
        if (File.Exists(localPath))
        {
            return localPath;
        }

        // Stop at this repository's solution boundary; never load an unrelated ancestor's secrets.
        for (DirectoryInfo? directory = new(contentRoot); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AdNoir.sln")))
            {
                return Path.Combine(directory.FullName, ".env");
            }
        }

        return localPath;
    }
}

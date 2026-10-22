using Microsoft.Extensions.Configuration;

namespace AdNoir.BuildingBlocks.Configuration;

/// <summary>
/// Reads a <c>.env</c> file into configuration.
/// </summary>
/// <remarks>
/// <para>
/// Under docker-compose, <c>.env</c> is injected through <c>env_file:</c> and ASP.NET Core's
/// environment-variable provider picks it up. Without a container runtime that path does not
/// exist, and nothing else reads <c>.env</c> - so every <c>AdNoir__*</c> setting would
/// silently be missing. This provider closes that gap without adding a package: the same
/// file works with and without Docker.
/// </para>
/// <para>
/// Register it in Development only. In Production the values come from real environment
/// variables, and a file sitting next to the binary must not be able to override them.
/// </para>
/// </remarks>
public sealed class DotEnvConfigurationProvider(DotEnvConfigurationSource source) : ConfigurationProvider
{
    private readonly DotEnvConfigurationSource _source = source;

    /// <inheritdoc />
    public override void Load()
    {
        string path = _source.Path;

        if (!File.Exists(path))
        {
            if (_source.Optional)
            {
                Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                return;
            }

            throw new FileNotFoundException($"The .env file '{path}' was not found.", path);
        }

        Data = Parse(File.ReadAllLines(path));
    }

    /// <summary>Parses <c>.env</c> lines into configuration keys.</summary>
    /// <param name="lines">The file's lines.</param>
    /// <returns>Configuration data with <c>__</c> translated to the section separator.</returns>
    internal static Dictionary<string, string?> Parse(IEnumerable<string> lines)
    {
        Dictionary<string, string?> data = new(StringComparer.OrdinalIgnoreCase);

        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();

            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            if (line.StartsWith("export ", StringComparison.Ordinal))
            {
                line = line["export ".Length..].TrimStart();
            }

            int separator = line.IndexOf('=', StringComparison.Ordinal);

            if (separator <= 0)
            {
                continue;
            }

            string key = line[..separator].Trim();
            string value = line[(separator + 1)..].Trim();

            if (key.Length == 0)
            {
                continue;
            }

            data[ToConfigurationKey(key)] = Unquote(value);
        }

        return data;
    }

    /// <summary>
    /// Translates the environment-variable form into a configuration path:
    /// <c>AdNoir__Mongo__ConnectionString</c> becomes <c>AdNoir:Mongo:ConnectionString</c>.
    /// </summary>
    private static string ToConfigurationKey(string key) =>
        key.Replace("__", ConfigurationPath.KeyDelimiter, StringComparison.Ordinal);

    /// <summary>
    /// Strips matching surrounding quotes and resolves escapes inside double quotes.
    /// </summary>
    /// <remarks>
    /// Inline comments are deliberately not supported. Treating everything after a <c>#</c>
    /// as a comment would quietly truncate any password or connection string containing one,
    /// and that failure surfaces as an authentication error far from its cause.
    /// </remarks>
    private static string Unquote(string value)
    {
        if (value.Length >= 2)
        {
            if (value[0] == '"' && value[^1] == '"')
            {
                return value[1..^1]
                    .Replace("\\n", "\n", StringComparison.Ordinal)
                    .Replace("\\r", "\r", StringComparison.Ordinal)
                    .Replace("\\t", "\t", StringComparison.Ordinal)
                    .Replace("\\\"", "\"", StringComparison.Ordinal);
            }

            if (value[0] == '\'' && value[^1] == '\'')
            {
                return value[1..^1];
            }
        }

        return value;
    }
}

/// <summary>Configuration source for a <c>.env</c> file.</summary>
public sealed class DotEnvConfigurationSource : IConfigurationSource
{
    /// <summary>Path to the file. Relative paths resolve against the content root.</summary>
    public string Path { get; set; } = ".env";

    /// <summary>Whether a missing file is acceptable. Defaults to <see langword="true"/>.</summary>
    public bool Optional { get; set; } = true;

    /// <inheritdoc />
    public IConfigurationProvider Build(IConfigurationBuilder builder) =>
        new DotEnvConfigurationProvider(this);
}

namespace AdNoir.IdentityAndOrg.Domain.Hierarchy;

/// <summary>
/// Builds and inspects hierarchy paths.
/// </summary>
/// <remarks>
/// <para>
/// A path always starts and ends with <c>/</c>: <c>/dealer-1/unit-a/station-5/</c>. The
/// trailing separator is not cosmetic - without it a prefix comparison would match
/// <c>/dealer-1</c> against <c>/dealer-10</c>. Even so, paths are for display and ordering
/// only; subtree queries run against the <c>Ancestors</c> array, which cannot collide at all.
/// </para>
/// <para>
/// Named in the plural because <c>HierarchyPath</c> is already a property on every
/// hierarchical entity; a type of the same name would be shadowed inside those classes.
/// </para>
/// </remarks>
public static class HierarchyPaths
{
    /// <summary>The path separator.</summary>
    public const char Separator = '/';

    /// <summary>The path of a virtual root, and the parent path of every root node.</summary>
    public const string Root = "/";

    /// <summary>
    /// Maximum path length. MongoDB's index key limit is 1024 bytes; a longer
    /// path would silently stop being indexed, turning subtree listings into collection scans.
    /// </summary>
    public const int MaxLength = 1024;

    /// <summary>Builds a child path from its parent's path and its own slug.</summary>
    /// <param name="parentPath">The parent's path, or null for a root node.</param>
    /// <param name="slug">The child's slug.</param>
    /// <returns>The child path, starting and ending with the separator.</returns>
    /// <exception cref="ArgumentException"><paramref name="slug"/> is null or whitespace.</exception>
    public static string Build(string? parentPath, string slug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        string parent = string.IsNullOrWhiteSpace(parentPath) ? Root : Normalize(parentPath);

        return parent + slug + Separator;
    }

    /// <summary>Ensures a path starts and ends with the separator.</summary>
    /// <param name="path">The path to normalise.</param>
    /// <returns>The normalised path.</returns>
    public static string Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Root;
        }

        string trimmed = path.Trim();

        if (trimmed[0] != Separator)
        {
            trimmed = Separator + trimmed;
        }

        if (trimmed[^1] != Separator)
        {
            trimmed += Separator;
        }

        return trimmed;
    }

    /// <summary>Whether a path is within the maximum length.</summary>
    /// <param name="path">The path to check.</param>
    /// <returns><see langword="true"/> when the path fits.</returns>
    public static bool IsWithinLimit(string path) =>
        !string.IsNullOrEmpty(path) && path.Length <= MaxLength;

    /// <summary>The slug segments of a path, outermost first.</summary>
    /// <param name="path">The path.</param>
    /// <returns>The segments, or an empty array for the root.</returns>
    public static IReadOnlyList<string> Segments(string? path) =>
        string.IsNullOrWhiteSpace(path)
            ? []
            : path.Split(Separator, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>The path of the parent of the given path.</summary>
    /// <param name="path">The path.</param>
    /// <returns>The parent path, or <see cref="Root"/> when the path is a root node.</returns>
    public static string ParentOf(string? path)
    {
        IReadOnlyList<string> segments = Segments(path);

        return segments.Count <= 1
            ? Root
            : Root + string.Join(Separator, segments.Take(segments.Count - 1)) + Separator;
    }

    /// <summary>
    /// Rewrites a descendant's path after its ancestor moved or was re-slugged.
    /// </summary>
    /// <param name="descendantPath">The descendant's current path.</param>
    /// <param name="oldAncestorPath">The ancestor's path before the change.</param>
    /// <param name="newAncestorPath">The ancestor's path after the change.</param>
    /// <returns>The descendant's new path.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="descendantPath"/> does not sit under <paramref name="oldAncestorPath"/>.
    /// </exception>
    public static string Rebase(string descendantPath, string oldAncestorPath, string newAncestorPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(descendantPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(oldAncestorPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(newAncestorPath);

        if (!descendantPath.StartsWith(oldAncestorPath, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Path '{descendantPath}' is not under '{oldAncestorPath}', so it cannot be rebased.",
                nameof(descendantPath));
        }

        return newAncestorPath + descendantPath[oldAncestorPath.Length..];
    }
}

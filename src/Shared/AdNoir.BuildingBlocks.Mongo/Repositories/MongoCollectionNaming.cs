using System.Globalization;

namespace AdNoir.BuildingBlocks.Mongo.Repositories;

/// <summary>
/// Derives a collection name from an entity type.
/// </summary>
/// <remarks>
/// Lower camel case and naively pluralised: <c>OrgUnit</c> becomes <c>orgUnits</c>,
/// <c>Category</c> becomes <c>categories</c>. The rules cover regular English nouns only,
/// which is deliberate - a full pluraliser would guess at irregular words and produce a
/// collection name nobody expects. Anything irregular passes an explicit name to the
/// repository instead.
/// </remarks>
public static class MongoCollectionNaming
{
    /// <summary>Returns the conventional collection name for an entity type.</summary>
    /// <param name="entityType">The entity type.</param>
    /// <returns>The collection name.</returns>
    public static string For(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        return Pluralize(ToCamelCase(entityType.Name));
    }

    private static string ToCamelCase(string name) =>
        name.Length == 0 || char.IsLower(name[0])
            ? name
            : string.Create(
                name.Length,
                name,
                static (span, source) =>
                {
                    source.AsSpan().CopyTo(span);
                    span[0] = char.ToLower(source[0], CultureInfo.InvariantCulture);
                });

    private static string Pluralize(string name)
    {
        if (name.Length == 0)
        {
            return name;
        }

        if (name.EndsWith('y') && name.Length > 1 && !IsVowel(name[^2]))
        {
            return string.Concat(name.AsSpan(0, name.Length - 1), "ies");
        }

        if (name.EndsWith('s') ||
            name.EndsWith('x') ||
            name.EndsWith('z') ||
            name.EndsWith("ch", StringComparison.Ordinal) ||
            name.EndsWith("sh", StringComparison.Ordinal))
        {
            return name + "es";
        }

        return name + "s";
    }

    private static bool IsVowel(char value) =>
        value is 'a' or 'e' or 'i' or 'o' or 'u' or 'A' or 'E' or 'I' or 'O' or 'U';
}

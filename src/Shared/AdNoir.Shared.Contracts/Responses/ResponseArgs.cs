namespace AdNoir.Shared.Contracts.Responses;

/// <summary>
/// Builds the named-placeholder argument dictionaries carried by responses and messages.
/// </summary>
/// <remarks>
/// <para>
/// An anonymous object (<c>new { userId }</c>) would require reflection to turn into a
/// dictionary: no compile-time safety, and a misspelled property name would only surface
/// as a missing placeholder at runtime. Named tuples give the same call-site brevity while
/// keeping the keys visible to the compiler.
/// </para>
/// <para>
/// Named <c>ResponseArgs</c> rather than <c>MessageArgs</c> because
/// <see cref="BaseResponse.MessageArgs"/> is a property of that exact name; a type sharing
/// it would be shadowed at every call site inside the response types.
/// </para>
/// </remarks>
public static class ResponseArgs
{
    /// <summary>
    /// Converts named tuple arguments into an argument dictionary, or <see langword="null"/>
    /// when there are none — an empty dictionary would be serialized as <c>{}</c> for no reason.
    /// </summary>
    /// <param name="args">Named placeholder values.</param>
    /// <returns>A read-only dictionary, or <see langword="null"/> when <paramref name="args"/> is empty.</returns>
    /// <exception cref="ArgumentException">A key is null or whitespace, or a key is duplicated.</exception>
    public static IReadOnlyDictionary<string, object?>? From(params (string Key, object? Value)[] args)
    {
        if (args is null || args.Length == 0)
        {
            return null;
        }

        var dictionary = new Dictionary<string, object?>(args.Length, StringComparer.Ordinal);

        foreach ((string key, object? value) in args)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            if (!dictionary.TryAdd(key, value))
            {
                throw new ArgumentException($"Duplicate message argument key '{key}'.", nameof(args));
            }
        }

        return dictionary;
    }
}

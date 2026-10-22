namespace AdNoir.Domain.Abstractions.Repositories;

/// <summary>
/// Marks a method as deliberately reading across tenant boundaries, with a written reason.
/// </summary>
/// <remarks>
/// An architecture test scans method bodies for calls into
/// <see cref="IUnsafeCrossTenantQuery{TEntity}"/> and fails the build when the calling method
/// is not marked with this attribute. The justification is required rather than optional
/// because the point of the attribute is to make the reason readable during review, not to
/// tick a box.
/// </remarks>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
public sealed class AllowsCrossTenantAccessAttribute : Attribute
{
    /// <summary>Initializes a new instance.</summary>
    /// <param name="justification">Why this method must see data from more than one tenant.</param>
    /// <exception cref="ArgumentException"><paramref name="justification"/> is null or whitespace.</exception>
    public AllowsCrossTenantAccessAttribute(string justification)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(justification);

        Justification = justification;
    }

    /// <summary>Why this method must see data from more than one tenant.</summary>
    public string Justification { get; }
}

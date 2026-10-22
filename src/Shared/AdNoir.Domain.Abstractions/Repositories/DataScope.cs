namespace AdNoir.Domain.Abstractions.Repositories;

/// <summary>
/// How much of a tenant's data a role assignment may see or change.
/// </summary>
/// <remarks>
/// The repository turns these into query filters automatically. A typical assignment reads
/// at <see cref="Subtree"/> and writes at <see cref="Unit"/>.
/// </remarks>
public enum DataScope
{
    /// <summary>
    /// Only records the user owns. Translated to an equality match on the entity's ownership
    /// field, normally <c>CreatedBy</c>. Requires the <c>org_unit_id</c> claim to be present.
    /// </summary>
    Self = 0,

    /// <summary>
    /// Records belonging to the user's own organisation unit. Translated to an equality match
    /// on the unit identifier.
    /// </summary>
    Unit = 1,

    /// <summary>
    /// The user's unit and everything beneath it. Translated to a match on the
    /// <see cref="Entities.IHierarchical.Ancestors"/> array, which is an equality lookup on a
    /// multikey index rather than a path prefix scan.
    /// </summary>
    Subtree = 2,

    /// <summary>Everything in the tenant. Translated to the tenant filter alone.</summary>
    Tenant = 3,
}

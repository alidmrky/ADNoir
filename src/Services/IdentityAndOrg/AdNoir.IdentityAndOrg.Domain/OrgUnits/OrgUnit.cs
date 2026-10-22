using AdNoir.Domain.Abstractions.Entities;
using AdNoir.Domain.Abstractions.Entities.Bases;
using AdNoir.IdentityAndOrg.Domain.Hierarchy;

namespace AdNoir.IdentityAndOrg.Domain.OrgUnits;

/// <summary>
/// A node in a tenant's organisation hierarchy.
/// </summary>
/// <remarks>
/// Inherits tenant ownership, audit fields, soft delete, optimistic concurrency and the
/// hierarchy fields from <see cref="HierarchicalTenantEntity"/>. Marked
/// <see cref="IAuditable"/>, so structural changes produce audit trail records.
/// </remarks>
public sealed class OrgUnit : HierarchicalTenantEntity, IAuditable
{
    /// <summary>Display name. Changing it does not by itself change the slug or the path.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// URL-safe segment used in <see cref="IHierarchical.HierarchyPath"/>. Unique among
    /// siblings, not globally.
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Optional external or human-assigned code, e.g. a branch number.</summary>
    public string? Code { get; set; }

    /// <summary>Whether the unit is currently in use.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Creates a root unit for a tenant.</summary>
    /// <param name="tenantId">The owning tenant.</param>
    /// <param name="name">Display name.</param>
    /// <param name="slug">URL-safe segment.</param>
    /// <param name="code">Optional external code.</param>
    /// <returns>The new root unit.</returns>
    public static OrgUnit CreateRoot(Guid tenantId, string name, string slug, string? code = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        return new OrgUnit
        {
            TenantId = tenantId,
            Name = name,
            Slug = slug,
            Code = code,
            ParentId = null,
            HierarchyPath = HierarchyPaths.Build(HierarchyPaths.Root, slug),
            Ancestors = [],
        };
    }

    /// <summary>Creates a child of an existing unit.</summary>
    /// <param name="parent">The parent unit.</param>
    /// <param name="name">Display name.</param>
    /// <param name="slug">URL-safe segment, unique among the parent's children.</param>
    /// <param name="code">Optional external code.</param>
    /// <returns>The new child unit.</returns>
    /// <remarks>
    /// Path and ancestors are derived here rather than assigned by the caller, so the two
    /// representations cannot drift apart at creation time.
    /// </remarks>
    public static OrgUnit CreateChild(OrgUnit parent, string name, string slug, string? code = null)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        return new OrgUnit
        {
            TenantId = parent.TenantId,
            Name = name,
            Slug = slug,
            Code = code,
            ParentId = parent.Id,
            HierarchyPath = HierarchyPaths.Build(parent.HierarchyPath, slug),
            Ancestors = [.. parent.Ancestors, parent.Id],
        };
    }

    /// <summary>Whether this unit is a root of its tenant's tree.</summary>
    public bool IsRoot => ParentId is null;

    /// <summary>Depth in the tree; a root is at depth zero.</summary>
    public int Depth => Ancestors.Count;
}

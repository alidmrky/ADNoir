namespace AdNoir.IdentityAndOrg.Application.OrgUnits.Dtos;

/// <summary>An organisation unit as exposed by the API.</summary>
/// <param name="Id">Unit identifier.</param>
/// <param name="Name">Display name.</param>
/// <param name="Slug">URL-safe segment used in the path.</param>
/// <param name="Code">Optional external code.</param>
/// <param name="ParentId">Parent unit, or null for a root.</param>
/// <param name="HierarchyPath">Readable path, e.g. <c>/head-office/istanbul/</c>.</param>
/// <param name="Ancestors">Identifiers of every ancestor, outermost first.</param>
/// <param name="Depth">Depth in the tree; a root is zero.</param>
/// <param name="IsActive">Whether the unit is in use.</param>
/// <param name="Version">Row version, echoed back on update for the concurrency check.</param>
public sealed record OrgUnitDto(
    Guid Id,
    string Name,
    string Slug,
    string? Code,
    Guid? ParentId,
    string HierarchyPath,
    IReadOnlyList<Guid> Ancestors,
    int Depth,
    bool IsActive,
    long Version);

/// <summary>Request to create an organisation unit.</summary>
/// <param name="Name">Display name.</param>
/// <param name="ParentId">
/// Parent unit, or null to create a root. Supplying a parent while the tenant has hierarchy
/// switched off is rejected.
/// </param>
/// <param name="Slug">
/// URL-safe segment. Leave null to derive it from <paramref name="Name"/>; a derived slug
/// gets a numeric suffix on collision, an explicit one is rejected instead.
/// </param>
/// <param name="Code">Optional external code.</param>
public sealed record CreateOrgUnitRequest(
    string Name,
    Guid? ParentId = null,
    string? Slug = null,
    string? Code = null);

/// <summary>Request to rename an organisation unit.</summary>
/// <param name="Name">New display name.</param>
/// <param name="Slug">
/// New slug, or null to keep the current one.
/// </param>
/// <param name="Code">New external code.</param>
/// <param name="ExpectedVersion">The version the caller last read.</param>
/// <remarks>
/// The slug is deliberately separate from the name and optional. Changing it rewrites the
/// <c>HierarchyPath</c> of the entire subtree - the trigger people most often forget - so it
/// should be an explicit request, not a side effect of correcting a typo in a display name.
/// </remarks>
public sealed record RenameOrgUnitRequest(
    string Name,
    string? Slug,
    string? Code,
    long ExpectedVersion);

/// <summary>Request to move an organisation unit to a different parent.</summary>
/// <param name="NewParentId">The new parent, or null to make it a root.</param>
/// <param name="ExpectedVersion">The version the caller last read.</param>
public sealed record MoveOrgUnitRequest(Guid? NewParentId, long ExpectedVersion);

using AdNoir.IdentityAndOrg.Domain.Tenants;

namespace AdNoir.IdentityAndOrg.Application.Tenants.Dtos;

/// <summary>
/// A tenant as exposed by the API.
/// </summary>
/// <remarks>
/// Separate from the <see cref="Tenant"/> entity on purpose: returning the entity
/// would make the storage schema the API contract and would expose <c>IsDeleted</c> and the
/// audit columns. <see cref="TenantStatus"/> is shared because an enum is a value, not a
/// storage shape.
/// </remarks>
/// <param name="Id">Tenant identifier.</param>
/// <param name="Name">Display name.</param>
/// <param name="Slug">URL-safe identifier.</param>
/// <param name="Status">Lifecycle state.</param>
/// <param name="HierarchyEnabled">Whether organisation hierarchy is switched on.</param>
/// <param name="TimeZoneId">IANA time zone.</param>
/// <param name="PermissionVersion">Version of the role-to-permission matrix.</param>
/// <param name="CreatedAtUtc">When the tenant was created.</param>
/// <param name="UpdatedAtUtc">When the tenant was last modified.</param>
/// <param name="Version">
/// Row version. Callers echo it back on update so a concurrent edit is detected instead of
/// being silently overwritten.
/// </param>
public sealed record TenantDto(
    Guid Id,
    string Name,
    string Slug,
    TenantStatus Status,
    bool HierarchyEnabled,
    string TimeZoneId,
    long PermissionVersion,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    long Version);

/// <summary>Request to create a tenant.</summary>
/// <param name="Name">Display name.</param>
/// <param name="Slug">
/// URL-safe identifier. Leave null to derive it from <paramref name="Name"/>; a derived slug
/// gets a numeric suffix on collision, an explicit one is rejected instead.
/// </param>
/// <param name="HierarchyEnabled">Whether to switch organisation hierarchy on.</param>
/// <param name="TimeZoneId">IANA time zone.</param>
public sealed record CreateTenantRequest(
    string Name,
    string? Slug = null,
    bool HierarchyEnabled = false,
    string TimeZoneId = "UTC");

/// <summary>Request to update a tenant.</summary>
/// <param name="Name">New display name.</param>
/// <param name="TimeZoneId">New IANA time zone.</param>
/// <param name="Status">New lifecycle state.</param>
/// <param name="ExpectedVersion">
/// The version the caller last read. Sent back so a concurrent edit produces a conflict
/// rather than a silent overwrite.
/// </param>
public sealed record UpdateTenantRequest(
    string Name,
    string TimeZoneId,
    TenantStatus Status,
    long ExpectedVersion);

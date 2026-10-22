using AdNoir.Domain.Abstractions.Entities;
using AdNoir.Domain.Abstractions.Entities.Bases;

namespace AdNoir.IdentityAndOrg.Domain.Tenants;

/// <summary>Lifecycle state of a tenant.</summary>
public enum TenantStatus
{
    /// <summary>Evaluating; usable but time-limited.</summary>
    Trial = 0,

    /// <summary>Normal operation.</summary>
    Active = 1,

    /// <summary>Sign-in is blocked, data is preserved.</summary>
    Suspended = 2,

    /// <summary>Marked for deletion; waiting out the retention period.</summary>
    PendingDeletion = 3,

    /// <summary>Deleted.</summary>
    Deleted = 4,
}

/// <summary>
/// A tenant. The root of the isolation boundary every other entity is filtered by.
/// </summary>
/// <remarks>
/// Deliberately does <strong>not</strong> implement <see cref="IMultiTenant"/>: a tenant does
/// not belong to a tenant. The generic repository checks for that interface before adding its
/// tenant filter, so this collection is naturally cross-tenant - which is correct, and is why
/// reading it requires a platform-level permission.
/// </remarks>
public sealed class Tenant : FullAuditedEntity, IAuditable
{
    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>URL-safe identifier, unique across the platform.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Lifecycle state.</summary>
    public TenantStatus Status { get; set; } = TenantStatus.Trial;

    /// <summary>
    /// Whether organisation hierarchy is switched on for this tenant.
    /// When false, queries filter on <c>TenantId</c> alone and the scope machinery stays out
    /// of the way.
    /// </summary>
    public bool HierarchyEnabled { get; set; }

    /// <summary>IANA time zone, used for presentation only; storage is always UTC.</summary>
    public string TimeZoneId { get; set; } = "UTC";

    /// <summary>
    /// Version of this tenant's role-to-permission matrix. Every service
    /// caches the matrix under a key containing this number, so incrementing it invalidates
    /// the cache everywhere without an eviction message.
    /// </summary>
    public long PermissionVersion { get; set; } = 1;

    /// <summary>Whether the tenant may currently be used.</summary>
    public bool IsUsable => Status is TenantStatus.Trial or TenantStatus.Active;

    /// <summary>Creates a tenant.</summary>
    /// <param name="name">Display name.</param>
    /// <param name="slug">URL-safe identifier.</param>
    /// <param name="hierarchyEnabled">Whether to switch hierarchy on.</param>
    /// <param name="timeZoneId">IANA time zone.</param>
    /// <returns>The new tenant, in <see cref="TenantStatus.Trial"/>.</returns>
    public static Tenant Create(
        string name,
        string slug,
        bool hierarchyEnabled = false,
        string timeZoneId = "UTC")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);

        return new Tenant
        {
            Name = name,
            Slug = slug,
            HierarchyEnabled = hierarchyEnabled,
            TimeZoneId = timeZoneId,
            Status = TenantStatus.Trial,
            PermissionVersion = 1,
        };
    }

    /// <summary>
    /// Increments the permission matrix version, invalidating every cached copy.
    /// </summary>
    public void BumpPermissionVersion() => PermissionVersion++;
}

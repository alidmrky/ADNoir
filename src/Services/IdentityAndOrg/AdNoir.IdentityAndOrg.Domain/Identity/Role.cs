using AdNoir.Domain.Abstractions.Entities;
using AdNoir.Domain.Abstractions.Entities.Bases;

namespace AdNoir.IdentityAndOrg.Domain.Identity;

/// <summary>
/// A role and the permissions it grants.
/// </summary>
/// <remarks>
/// <para>
/// A role normally belongs to a tenant. System roles - <c>SuperAdmin</c>, <c>TenantAdmin</c> -
/// are global and carry a null <see cref="TenantId"/>. This type does not implement
/// <see cref="IMultiTenant"/>; the role repository must explicitly restrict every generic
/// query to the current tenant and global system roles.
/// </para>
/// <para>
/// Permissions live here and nowhere else. They are never written into a token; a token
/// carries role codes and the matrix version, and each service resolves the two through its
/// cached copy of the matrix.
/// </para>
/// </remarks>
public sealed class Role : FullAuditedEntity, IAuditable
{
    /// <summary>The owning tenant, or null for a global system role.</summary>
    public Guid? TenantId { get; set; }

    /// <summary>Stable code used in tokens and assignments, e.g. <c>TenantAdmin</c>.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Permissions granted, in <c>module:action</c> form.</summary>
    public IReadOnlyList<string> Permissions { get; set; } = [];

    /// <summary>
    /// Whether this is a built-in role. System roles are created by the migrator and must not
    /// be deleted or renamed, because tokens and seed data reference their codes.
    /// </summary>
    public bool IsSystemRole { get; set; }

    /// <summary>Creates a tenant-owned role.</summary>
    /// <param name="tenantId">The owning tenant.</param>
    /// <param name="code">Role code.</param>
    /// <param name="name">Display name.</param>
    /// <param name="permissions">Permissions granted.</param>
    /// <returns>The new role.</returns>
    public static Role CreateForTenant(
        Guid tenantId,
        string code,
        string name,
        IReadOnlyList<string> permissions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(permissions);

        return new Role
        {
            TenantId = tenantId,
            Code = code,
            Name = name,
            Permissions = permissions,
            IsSystemRole = false,
        };
    }

    /// <summary>Creates a global system role.</summary>
    /// <param name="code">Role code.</param>
    /// <param name="name">Display name.</param>
    /// <param name="permissions">Permissions granted.</param>
    /// <returns>The new role.</returns>
    public static Role CreateSystemRole(string code, string name, IReadOnlyList<string> permissions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(permissions);

        return new Role
        {
            TenantId = null,
            Code = code,
            Name = name,
            Permissions = permissions,
            IsSystemRole = true,
        };
    }
}

/// <summary>Built-in role codes, seeded by the migrator.</summary>
public static class SystemRoles
{
    /// <summary>Platform administrator. Not scoped to a tenant.</summary>
    public const string SuperAdmin = "SuperAdmin";

    /// <summary>Administrator of a single tenant.</summary>
    public const string TenantAdmin = "TenantAdmin";
}

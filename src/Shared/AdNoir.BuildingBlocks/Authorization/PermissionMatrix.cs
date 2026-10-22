using System.Collections.Frozen;

namespace AdNoir.BuildingBlocks.Authorization;

/// <summary>
/// A tenant's role-to-permission map at a given version.
/// </summary>
/// <remarks>
/// Permissions are not written into the JWT; the token carries role codes and
/// <c>perm_ver</c> only. Each service keeps this matrix in HybridCache keyed by tenant and
/// version, so a permission check is a dictionary lookup with no network call. When the
/// matrix changes the version increments, the cache key changes with it, and invalidation
/// happens by itself - no cross-service eviction message to get wrong.
/// </remarks>
public sealed class PermissionMatrix
{
    private readonly FrozenDictionary<string, FrozenSet<string>> _rolePermissions;

    /// <summary>Initializes a new instance.</summary>
    /// <param name="tenantId">The tenant this matrix belongs to.</param>
    /// <param name="version">The matrix version, matching the token's <c>perm_ver</c>.</param>
    /// <param name="rolePermissions">Permissions granted by each role code.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rolePermissions"/> is null.</exception>
    public PermissionMatrix(
        Guid tenantId,
        long version,
        IReadOnlyDictionary<string, IReadOnlyList<string>> rolePermissions)
    {
        ArgumentNullException.ThrowIfNull(rolePermissions);

        TenantId = tenantId;
        Version = version;

        _rolePermissions = rolePermissions.ToFrozenDictionary(
            pair => pair.Key,
            pair => pair.Value.ToFrozenSet(StringComparer.Ordinal),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The tenant this matrix belongs to.</summary>
    public Guid TenantId { get; }

    /// <summary>The matrix version.</summary>
    public long Version { get; }

    /// <summary>Role codes present in this matrix.</summary>
    public IReadOnlyCollection<string> Roles => _rolePermissions.Keys;

    /// <summary>Whether any of the caller's roles grants a permission.</summary>
    /// <param name="roles">The caller's role codes.</param>
    /// <param name="permission">The permission to check.</param>
    /// <returns><see langword="true"/> when at least one role grants it.</returns>
    public bool Grants(IEnumerable<string> roles, string permission)
    {
        ArgumentNullException.ThrowIfNull(roles);

        foreach (string role in roles)
        {
            if (_rolePermissions.TryGetValue(role, out FrozenSet<string>? permissions) &&
                permissions.Contains(permission))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>All permissions granted by a role.</summary>
    /// <param name="role">The role code.</param>
    /// <returns>The permissions, or an empty set when the role is unknown.</returns>
    public IReadOnlyCollection<string> PermissionsOf(string role) =>
        _rolePermissions.TryGetValue(role, out FrozenSet<string>? permissions)
            ? permissions
            : FrozenSet<string>.Empty;
}

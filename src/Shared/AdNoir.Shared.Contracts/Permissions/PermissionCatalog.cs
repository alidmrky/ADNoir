using System.Collections.ObjectModel;
using System.Reflection;

namespace AdNoir.Shared.Contracts.Permissions;

/// <summary>
/// The single catalog of permissions. Permissions are constant strings in
/// <c>module:action</c> form and live here so that both the services that check them and the
/// migrator that seeds them read the same list.
/// </summary>
/// <remarks>
/// Permissions are never written into the JWT. The token carries only role codes and the
/// tenant's permission-matrix version (<c>perm_ver</c>); each service keeps the role-to-permission
/// map in HybridCache keyed by tenant and version, so a permission check is a dictionary
/// lookup with no network call, and bumping the version invalidates the cache by itself.
/// </remarks>
public static class PermissionCatalog
{
    /// <summary>Tenant administration.</summary>
    public static class Tenants
    {
        /// <summary>Read tenant records.</summary>
        public const string Read = "tenant:read";

        /// <summary>Create, update and configure tenants.</summary>
        public const string Manage = "tenant:manage";
    }

    /// <summary>Organisation units and their hierarchy.</summary>
    public static class OrgUnits
    {
        /// <summary>Read organisation units.</summary>
        public const string Read = "orgunit:read";

        /// <summary>Create and update organisation units.</summary>
        public const string Write = "orgunit:write";

        /// <summary>Move or rename units, which rewrites the subtree.</summary>
        public const string Manage = "orgunit:manage";
    }

    /// <summary>User accounts.</summary>
    public static class Users
    {
        /// <summary>Read user records.</summary>
        public const string Read = "user:read";

        /// <summary>Create and update user records.</summary>
        public const string Write = "user:write";

        /// <summary>Assign roles and reset credentials.</summary>
        public const string Manage = "user:manage";
    }

    /// <summary>Roles and the permission matrix.</summary>
    public static class Roles
    {
        /// <summary>Read roles and their permissions.</summary>
        public const string Read = "role:read";

        /// <summary>Create roles and change the permission matrix.</summary>
        public const string Manage = "role:manage";
    }

    /// <summary>Audit trail.</summary>
    public static class Audit
    {
        /// <summary>Read audit log entries.</summary>
        public const string Read = "audit:read";
    }

    /// <summary>Sample items (demonstration service).</summary>
    public static class Items
    {
        /// <summary>Read sample items.</summary>
        public const string Read = "item:read";

        /// <summary>Create and update sample items.</summary>
        public const string Write = "item:write";
    }

    /// <summary>Tenant notification administration.</summary>
    public static class Notifications
    {
        /// <summary>Read masked delivery history and aggregate statistics.</summary>
        public const string Read = "notification:read";
    }

    private static readonly Lazy<IReadOnlyList<string>> AllPermissions = new(Discover);

    /// <summary>
    /// Every permission declared in this catalog. Used by the migrator to seed the permission
    /// list and by tests to assert that no permission is checked without being declared.
    /// </summary>
    /// <returns>All permission strings, ordered.</returns>
    public static IReadOnlyList<string> All() => AllPermissions.Value;

    /// <summary>Whether the given string is a declared permission.</summary>
    /// <param name="permission">The permission to look for.</param>
    /// <returns><see langword="true"/> when the permission exists in this catalog.</returns>
    public static bool Exists(string permission) =>
        AllPermissions.Value.Contains(permission, StringComparer.Ordinal);

    private static ReadOnlyCollection<string> Discover()
    {
        List<string> permissions = [];

        foreach (Type group in typeof(PermissionCatalog).GetNestedTypes(BindingFlags.Public))
        {
            foreach (FieldInfo field in group.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field is { IsLiteral: true, IsInitOnly: false } &&
                    field.GetRawConstantValue() is string value)
                {
                    permissions.Add(value);
                }
            }
        }

        permissions.Sort(StringComparer.Ordinal);
        return permissions.AsReadOnly();
    }
}

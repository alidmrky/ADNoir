using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace AdNoir.BuildingBlocks.Authorization;

/// <summary>
/// Endpoint helpers for permission-based authorization.
/// </summary>
public static class AuthorizationEndpointExtensions
{
    /// <summary>Requires a permission for this endpoint.</summary>
    /// <param name="builder">The endpoint builder.</param>
    /// <param name="permission">The permission, from <c>PermissionCatalog</c>.</param>
    /// <returns>The same builder.</returns>
    public static RouteHandlerBuilder RequirePermission(this RouteHandlerBuilder builder, string permission)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);

        return builder.RequireAuthorization(PermissionPolicyProvider.PolicyNameFor(permission));
    }

    /// <summary>Requires a permission for every endpoint in this group.</summary>
    /// <param name="builder">The group builder.</param>
    /// <param name="permission">The permission, from <c>PermissionCatalog</c>.</param>
    /// <returns>The same builder.</returns>
    public static RouteGroupBuilder RequirePermission(this RouteGroupBuilder builder, string permission)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);

        return builder.RequireAuthorization(PermissionPolicyProvider.PolicyNameFor(permission));
    }
}

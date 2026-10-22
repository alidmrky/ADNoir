using Microsoft.AspNetCore.Authorization;

namespace AdNoir.BuildingBlocks.Authorization;

/// <summary>
/// Requires a single permission, in <c>module:action</c> form.
/// </summary>
/// <param name="permission">The required permission, e.g. <c>orgunit:write</c>.</param>
public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    /// <summary>The required permission.</summary>
    public string Permission { get; } = permission;

    /// <inheritdoc />
    public override string ToString() => $"Permission:{Permission}";
}

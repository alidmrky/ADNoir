using AdNoir.Domain.Abstractions.Repositories;

namespace AdNoir.BuildingBlocks.Context;

/// <summary>
/// The caller for work with no user behind it: the migrator, background jobs, message
/// consumers.
/// </summary>
/// <remarks>
/// Reports itself as unauthenticated, so audit fields are left as system-written rather than
/// attributed to whoever happened to trigger the process. The scopes are set to
/// <see cref="DataScope.Tenant"/> because this code operates on a whole tenant by design -
/// but it still cannot cross tenants: that needs <c>IUnsafeCrossTenantQuery</c> and a written
/// justification, exactly as it does for a request.
/// </remarks>
public sealed class SystemCurrentUser : ICurrentUser
{
    /// <inheritdoc />
    public bool IsAuthenticated => false;

    /// <inheritdoc />
    public Guid UserId =>
        throw new InvalidOperationException(
            "System processes have no user identity. Audit fields are written as null for them.");

    /// <inheritdoc />
    public string? Email => null;

    /// <inheritdoc />
    public Guid? OrgUnitId => null;

    /// <inheritdoc />
    public IReadOnlyList<string> Roles => [];

    /// <inheritdoc />
    public long PermissionVersion => 0;

    /// <inheritdoc />
    public string? SecurityStamp => null;

    /// <inheritdoc />
    public string? HierarchyPath => null;

    /// <inheritdoc />
    public DataScope ReadScope => DataScope.Tenant;

    /// <inheritdoc />
    public DataScope WriteScope => DataScope.Tenant;
}

using AdNoir.Domain.Abstractions.Repositories;

namespace AdNoir.BuildingBlocks.Context;

/// <summary>
/// The authenticated caller. Registered as a scoped service and populated from the access
/// token's claims.
/// </summary>
public interface ICurrentUser
{
    /// <summary>Whether the caller is authenticated.</summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// The caller's user identifier.
    /// </summary>
    /// <exception cref="InvalidOperationException">The caller is not authenticated.</exception>
    Guid UserId { get; }

    /// <summary>The caller's e-mail address, when the token carries one.</summary>
    string? Email { get; }

    /// <summary>
    /// The caller's organisation unit. Null only for callers outside the hierarchy; the
    /// <see cref="DataScope.Self"/> and <see cref="DataScope.Unit"/> scopes cannot be applied
    /// without it.
    /// </summary>
    Guid? OrgUnitId { get; }

    /// <summary>The caller's role codes.</summary>
    IReadOnlyList<string> Roles { get; }

    /// <summary>Version of the tenant's permission matrix as of token issue.</summary>
    long PermissionVersion { get; }

    /// <summary>The caller's security stamp as of token issue.</summary>
    string? SecurityStamp { get; }

    /// <summary>The caller's hierarchy path, for display.</summary>
    string? HierarchyPath { get; }

    /// <summary>How much data the caller may read. Applied by the repository automatically.</summary>
    DataScope ReadScope { get; }

    /// <summary>How much data the caller may change. Applied by the repository automatically.</summary>
    DataScope WriteScope { get; }
}

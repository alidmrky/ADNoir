using AdNoir.Domain.Abstractions.Repositories;

namespace AdNoir.IdentityAndOrg.Domain.Identity;

/// <summary>User persistence.</summary>
public interface IUserRepository : IRepository<User>
{
    /// <summary>Loads an account for credential/token processing within the resolved tenant, without caller data scope.</summary>
    Task<User?> FindByIdForAuthenticationAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Persists authentication state within the resolved tenant, retaining version checks.</summary>
    Task UpdateForAuthenticationAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>Finds a user by normalised e-mail within the current tenant.</summary>
    /// <param name="normalizedEmail">The upper-cased address.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user, or null.</returns>
    Task<User?> FindByNormalizedEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken = default);

    /// <summary>Whether an e-mail is already registered in the current tenant.</summary>
    /// <param name="normalizedEmail">The upper-cased address.</param>
    /// <param name="excludingId">A user to ignore, used when updating that user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when the address is taken.</returns>
    Task<bool> EmailExistsAsync(
        string normalizedEmail,
        Guid? excludingId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Users holding a given role, used when a role's permissions change.</summary>
    /// <param name="roleId">The role.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The users.</returns>
    Task<IReadOnlyList<User>> ListByRoleAsync(Guid roleId, CancellationToken cancellationToken = default);
}

/// <summary>Role persistence.</summary>
public interface IRoleRepository : IRepository<Role>
{
    /// <summary>
    /// Every role available to a tenant: its own roles plus the global system roles.
    /// </summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The roles.</returns>
    Task<IReadOnlyList<Role>> ListForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Finds a role by code, looking at the tenant's roles and the system roles.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="code">The role code.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The role, or null.</returns>
    Task<Role?> FindByCodeAsync(Guid tenantId, string code, CancellationToken cancellationToken = default);
}

/// <summary>Refresh token persistence.</summary>
public interface IRefreshTokenRepository : IRepository<RefreshToken>
{
    /// <summary>Finds a token record by its hash.</summary>
    /// <param name="tokenHash">The hex-encoded SHA-256 hash.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The record, or null.</returns>
    /// <remarks>
    /// Returns revoked and expired records too. Reuse detection depends on finding a token
    /// that is no longer usable - filtering those out would hide the very event that matters.
    /// </remarks>
    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>Revokes every token in a family in one operation.</summary>
    /// <param name="familyId">The family.</param>
    /// <param name="reason">Why.</param>
    /// <param name="utcNow">The current time, in UTC.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>How many records were revoked.</returns>
    Task<long> RevokeFamilyAsync(
        Guid familyId,
        RefreshTokenRevocationReason reason,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    /// <summary>Revokes every active token belonging to a user.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="reason">Why.</param>
    /// <param name="utcNow">The current time, in UTC.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>How many records were revoked.</returns>
    Task<long> RevokeAllForUserAsync(
        Guid userId,
        RefreshTokenRevocationReason reason,
        DateTime utcNow,
        CancellationToken cancellationToken = default);
}

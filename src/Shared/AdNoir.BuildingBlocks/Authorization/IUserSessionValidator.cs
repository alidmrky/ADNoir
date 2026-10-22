namespace AdNoir.BuildingBlocks.Authorization;

/// <summary>Checks live user state; failures must never authorize a request.</summary>
public interface IUserSessionValidator
{
    /// <summary>Validates tenant, user, security stamp and permission version.</summary>
    Task<UserSessionState> ValidateAsync(Guid tenantId, Guid userId, string stamp, long permissionVersion, CancellationToken cancellationToken);
}

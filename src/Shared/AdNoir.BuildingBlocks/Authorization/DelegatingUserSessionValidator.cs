namespace AdNoir.BuildingBlocks.Authorization;

/// <summary>Adapts an infrastructure transport without making shared authorization depend on gRPC.</summary>
public sealed class DelegatingUserSessionValidator(
    Func<Guid, Guid, string, long, CancellationToken, Task<UserSessionState>> validate) : IUserSessionValidator
{
    /// <inheritdoc />
    public Task<UserSessionState> ValidateAsync(Guid tenantId, Guid userId, string stamp, long permissionVersion, CancellationToken cancellationToken) =>
        validate(tenantId, userId, stamp, permissionVersion, cancellationToken);
}

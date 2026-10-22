using AdNoir.Domain.Abstractions.Exceptions;
using AdNoir.Shared.Contracts.Messages;

namespace AdNoir.BuildingBlocks.Authorization;

/// <summary>
/// The default <see cref="IPermissionMatrixProvider"/>: refuses every request because no real
/// provider has been registered.
/// </summary>
/// <remarks>
/// <para>
/// The real provider fetches the matrix from Identity over gRPC and caches it. Until a
/// service registers one, something has to satisfy the authorization handler's dependency,
/// and the only safe answer is one that denies.
/// </para>
/// <para>
/// Two alternatives were rejected. Leaving the interface unregistered makes the host fail at
/// startup with a dependency-injection error, which says nothing about authorization and
/// invites someone to "fix" it by deleting the handler. Returning an empty matrix would let
/// requests through wherever a permission check happened to be absent - exactly the kind of
/// silent hole the fail-closed authorization rule exists to prevent.
/// </para>
/// <para>
/// So this throws <see cref="ExternalServiceException"/>, which the handler already treats as
/// fail-closed: the request is rejected with a message code that names the actual problem.
/// A service that registers its own provider replaces this one.
/// </para>
/// </remarks>
public sealed class UnavailablePermissionMatrixProvider : IPermissionMatrixProvider
{
    /// <inheritdoc />
    public Task<PermissionMatrix> GetAsync(
        Guid tenantId,
        long version,
        CancellationToken cancellationToken = default) =>
        throw new ExternalServiceException(
            MessageCodes.Auth.PermissionMatrixUnavailable,
            serviceName: "identity",
            message:
            $"No permission matrix provider is registered, so the matrix for tenant {tenantId} " +
            $"(version {version}) cannot be loaded. Register the gRPC-backed provider; " +
            "authorization deliberately fails closed until then.");
}

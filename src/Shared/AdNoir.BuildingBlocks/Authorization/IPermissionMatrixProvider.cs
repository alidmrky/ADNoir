namespace AdNoir.BuildingBlocks.Authorization;

/// <summary>
/// Supplies a tenant's permission matrix.
/// </summary>
/// <remarks>
/// <para>
/// The cache needs a cold-start path, and this is it. When a service starts, or the matrix
/// version changes, nothing is cached and the matrix must be fetched from Identity over gRPC
/// (<c>GetPermissionMatrix</c>). Omitting that RPC is an easy oversight that makes
/// authorization fail on the very first request of every deployment.
/// </para>
/// <para>
/// <strong>Failures must not open the door.</strong> If Identity is unreachable, the
/// implementation throws
/// <see cref="Domain.Abstractions.Exceptions.ExternalServiceException"/> and the request is
/// rejected. "Let it through just this once" would be a silent authorization hole, so the
/// fail-closed behaviour is fixed by a test rather than left to discipline.
/// </para>
/// </remarks>
public interface IPermissionMatrixProvider
{
    /// <summary>Gets the permission matrix for a tenant at a version.</summary>
    /// <param name="tenantId">The tenant.</param>
    /// <param name="version">The matrix version from the caller's token.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matrix.</returns>
    /// <exception cref="Domain.Abstractions.Exceptions.ExternalServiceException">
    /// The matrix could not be obtained. The caller must reject the request.
    /// </exception>
    Task<PermissionMatrix> GetAsync(Guid tenantId, long version, CancellationToken cancellationToken = default);
}

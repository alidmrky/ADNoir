using AdNoir.BuildingBlocks.Authorization;
using AdNoir.Domain.Abstractions.Exceptions;
using AdNoir.IdentityAndOrg.Grpc;
using AdNoir.Shared.Contracts.Messages;
using Grpc.Core;

namespace AdNoir.Sample.Infrastructure.Identity;

/// <summary>Loads an exact tenant permission version from Identity, denying on failure.</summary>
public sealed class GrpcPermissionMatrixProvider(UserService.UserServiceClient client) : IPermissionMatrixProvider
{
    /// <inheritdoc />
    public async Task<PermissionMatrix> GetAsync(Guid tenantId, long version, CancellationToken cancellationToken = default)
    {
        try
        {
            // Cache misses need the full matrix, not an unchanged response.
            using AsyncUnaryCall<GetPermissionMatrixResponse> call = client.GetPermissionMatrixAsync(
                new GetPermissionMatrixRequest { TenantId = tenantId.ToString(), KnownVersion = -1 },
                deadline: DateTime.UtcNow.AddSeconds(10), cancellationToken: cancellationToken);
            GetPermissionMatrixResponse response = await call.ResponseAsync;
            if (!Guid.TryParse(response.TenantId, out Guid actualTenant) || actualTenant != tenantId ||
                response.Version != version || response.Unchanged)
            {
                throw Unavailable("Identity returned a different tenant/version or an incomplete permission matrix.");
            }

            Dictionary<string, IReadOnlyList<string>> roles = new(StringComparer.OrdinalIgnoreCase);
            foreach (RolePermissions role in response.Roles)
            {
                if (string.IsNullOrWhiteSpace(role.RoleCode) || !roles.TryAdd(role.RoleCode, role.Permissions.ToArray()))
                {
                    throw Unavailable("Identity returned an invalid or duplicate role code.");
                }
            }

            return new PermissionMatrix(tenantId, version, roles);
        }
        catch (RpcException exception) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException("Permission lookup was cancelled.", exception, cancellationToken);
        }
        catch (RpcException exception)
        {
            throw Unavailable("Identity permission lookup failed.", exception);
        }
    }

    private static ExternalServiceException Unavailable(string message, Exception? innerException = null) =>
        new(MessageCodes.Auth.PermissionMatrixUnavailable, "identity", message, innerException);
}

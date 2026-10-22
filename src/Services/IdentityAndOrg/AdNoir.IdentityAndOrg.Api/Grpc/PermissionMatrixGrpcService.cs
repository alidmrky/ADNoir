using AdNoir.BuildingBlocks.Authorization;
using AdNoir.BuildingBlocks.Context;
using AdNoir.Shared.Contracts.Messages;
using AdNoir.IdentityAndOrg.Domain.Identity;
using AdNoir.IdentityAndOrg.Domain.Tenants;
using AdNoir.IdentityAndOrg.Grpc;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace AdNoir.IdentityAndOrg.Api.Grpc;

/// <summary>
/// Serves the permission matrix to other services.
/// </summary>
/// <remarks>
/// <para>
/// This is the cold-start path for every other service's permission cache. Without it,
/// authorization elsewhere fails on the first request after each deployment.
/// </para>
/// <para>
/// <c>BaseResponse</c> is not used here: the envelope is an HTTP contract, and on gRPC an
/// error is an <see cref="RpcException"/> with the code carried in trailer metadata.
/// </para>
/// </remarks>
public sealed partial class PermissionMatrixGrpcService(
    IRoleRepository roleRepository,
    ITenantRepository tenantRepository,
    PermissionMatrixCallerGuard callerGuard,
    IUserSessionValidator sessionValidator,
    ITenantContext tenantContext,
    ILogger<PermissionMatrixGrpcService> logger) : UserService.UserServiceBase
{
    /// <summary>Metadata key carrying the message code of a failure.</summary>
    public const string ErrorCodeTrailer = "adnoir-error-code";

    private readonly IRoleRepository _roleRepository = roleRepository;
    private readonly ITenantRepository _tenantRepository = tenantRepository;
    private readonly ILogger<PermissionMatrixGrpcService> _logger = logger;

    /// <inheritdoc />
    public override async Task<GetPermissionMatrixResponse> GetPermissionMatrix(
        GetPermissionMatrixRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        if (!Guid.TryParse(request.TenantId, out Guid tenantId) || tenantId == Guid.Empty)
        {
            throw Failure(StatusCode.InvalidArgument, MessageCodes.Tenant.NotResolved);
        }

        callerGuard.Authorize(context, tenantId);

        CancellationToken cancellationToken = context.CancellationToken;

        Tenant? tenant = await _tenantRepository.FindByIdAsync(tenantId, cancellationToken);

        if (tenant is null || !tenant.IsUsable)
        {
            throw Failure(StatusCode.NotFound, MessageCodes.Tenant.NotFound);
        }

        if (request.KnownVersion == tenant.PermissionVersion)
        {
            // The caller is already current. Answering without the role data keeps a
            // revalidation cheap, which matters because this runs on cache misses.
            return new GetPermissionMatrixResponse
            {
                TenantId = tenant.Id.ToString(),
                Version = tenant.PermissionVersion,
                Unchanged = true,
            };
        }

        IReadOnlyList<Role> roles = await _roleRepository.ListForTenantAsync(tenantId, cancellationToken);

        // The tenant version must still describe the roles just read.
        Tenant? current = await _tenantRepository.FindByIdAsync(tenantId, cancellationToken);
        if (current is null || !current.IsUsable || current.PermissionVersion != tenant.PermissionVersion)
        {
            throw Failure(StatusCode.Unavailable, MessageCodes.Auth.PermissionMatrixUnavailable);
        }

        GetPermissionMatrixResponse response = new()
        {
            TenantId = tenant.Id.ToString(),
            Version = tenant.PermissionVersion,
            Unchanged = false,
        };

        foreach (Role role in roles)
        {
            RolePermissions entry = new() { RoleCode = role.Code };
            entry.Permissions.AddRange(role.Permissions);
            response.Roles.Add(entry);
        }

        _logger.LogServedPermissionMatrix(tenantId, tenant.PermissionVersion, response.Roles.Count);

        return response;
    }

    private static RpcException Failure(StatusCode code, string messageCode)
    {
        Metadata trailers = new()
        {
            { ErrorCodeTrailer, messageCode },
        };

        return new RpcException(new Status(code, messageCode), trailers);
    }
}

/// <summary>Static LoggerMessage definitions for <see cref="PermissionMatrixGrpcService"/> (CA1873).</summary>
internal static partial class PermissionMatrixGrpcServiceLogMessages
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Served permission matrix for tenant {TenantId} version {Version} with {RoleCount} roles.")]
    public static partial void LogServedPermissionMatrix(this ILogger logger, Guid tenantId, long version, int roleCount);
}

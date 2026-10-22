using AdNoir.BuildingBlocks.Authorization;
using AdNoir.BuildingBlocks.Context;
using AdNoir.IdentityAndOrg.Grpc;
using AdNoir.Shared.Contracts.Messages;
using Grpc.Core;


namespace AdNoir.IdentityAndOrg.Api.Grpc;

public sealed partial class PermissionMatrixGrpcService
{
    /// <inheritdoc />
    public override async Task<ValidateUserSessionResponse> ValidateUserSession(ValidateUserSessionRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        if (!Guid.TryParse(request.TenantId, out Guid tenantId) || tenantId == Guid.Empty ||
            !Guid.TryParse(request.UserId, out Guid userId) || userId == Guid.Empty || request.SecurityStamp.Length > 128)
        {
            throw Failure(StatusCode.InvalidArgument, MessageCodes.Auth.TokenInvalid);
        }
        callerGuard.Authorize(context, tenantId, validateSession: true);

        tenantContext.SetTenant(tenantId, hierarchyEnabled: false);
        UserSessionState state = await sessionValidator.ValidateAsync(
            tenantId, userId, request.SecurityStamp, request.PermissionVersion, context.CancellationToken);
        return new ValidateUserSessionResponse { State = (int)state };
    }
}

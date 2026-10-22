using Microsoft.AspNetCore.Http;
using System.Globalization;
using AdNoir.BuildingBlocks.Context;
using AdNoir.Domain.Abstractions.Exceptions;
using AdNoir.Shared.Contracts.Messages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace AdNoir.BuildingBlocks.Authorization;

/// <summary>
/// The one handler behind every <c>.RequirePermission(...)</c> call.
/// </summary>
public sealed class PermissionAuthorizationHandler(
    IPermissionMatrixProvider matrixProvider,
    ILogger<PermissionAuthorizationHandler> logger) : AuthorizationHandler<PermissionRequirement>
{
    /// <summary>
    /// Failure reason used when the matrix itself could not be loaded, as opposed to the
    /// caller simply lacking the permission. The result handler turns this into a 502 rather
    /// than a 403, because "we could not check" is not the same answer as "no".
    /// </summary>
    public const string MatrixUnavailableReason = MessageCodes.Auth.PermissionMatrixUnavailable;

    private readonly IPermissionMatrixProvider _matrixProvider = matrixProvider;
    private readonly ILogger<PermissionAuthorizationHandler> _logger = logger;

    /// <inheritdoc />
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);

        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        if (!TryReadTenant(context, out Guid tenantId) ||
            !TryReadPermissionVersion(context, out long version))
        {
            context.Fail(new AuthorizationFailureReason(this, MessageCodes.Tenant.NotResolved));
            return;
        }

        IReadOnlyList<string> roles =
            [.. context.User.FindAll(AdNoirClaimTypes.Roles).Select(claim => claim.Value)];

        if (roles.Count == 0)
        {
            context.Fail(new AuthorizationFailureReason(this, MessageCodes.Auth.PermissionDenied));
            return;
        }

        PermissionMatrix matrix;

        try
        {
            matrix = await _matrixProvider.GetAsync(tenantId, version, (context.Resource as HttpContext)?.RequestAborted ?? CancellationToken.None);
        }
        catch (ExternalServiceException exception)
        {
            // Fail closed. Treating an unreachable Identity as "probably allowed" would be a
            // silent authorization hole, so the request is refused and the reason recorded.
            _logger.LogError(
                exception,
                "Permission matrix unavailable for tenant {TenantId} version {Version}; denying request.",
                tenantId,
                version);

            context.Fail(new AuthorizationFailureReason(this, MatrixUnavailableReason));
            return;
        }

        if (matrix.Grants(roles, requirement.Permission))
        {
            context.Succeed(requirement);
            return;
        }

        context.Fail(new AuthorizationFailureReason(this, MessageCodes.Auth.PermissionDenied));
    }

    private static bool TryReadTenant(AuthorizationHandlerContext context, out Guid tenantId) =>
        Guid.TryParse(context.User.FindFirst(AdNoirClaimTypes.TenantId)?.Value, out tenantId);

    private static bool TryReadPermissionVersion(AuthorizationHandlerContext context, out long version) =>
        long.TryParse(
            context.User.FindFirst(AdNoirClaimTypes.PermissionVersion)?.Value,
            CultureInfo.InvariantCulture,
            out version);
}


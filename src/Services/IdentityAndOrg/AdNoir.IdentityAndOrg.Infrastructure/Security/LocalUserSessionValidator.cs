using AdNoir.BuildingBlocks.Authorization;
using AdNoir.BuildingBlocks.Context;
using AdNoir.IdentityAndOrg.Domain.Identity;
using AdNoir.IdentityAndOrg.Domain.Tenants;

namespace AdNoir.IdentityAndOrg.Infrastructure.Security;

/// <summary>Reads authoritative account state in the explicitly resolved tenant scope.</summary>
public sealed class LocalUserSessionValidator(ITenantRepository tenants, IUserRepository users,
    ITenantContext tenantContext, TimeProvider clock) : IUserSessionValidator
{
    /// <inheritdoc />
    public async Task<UserSessionState> ValidateAsync(Guid tenantId, Guid userId, string stamp, long permissionVersion, CancellationToken cancellationToken)
    {
        if (!tenantContext.HasTenant || tenantContext.TenantId != tenantId)
        {
            return UserSessionState.Invalid;
        }
        Tenant? tenant = await tenants.FindByIdAsync(tenantId, cancellationToken);
        if (tenant is null || !tenant.IsUsable || tenant.PermissionVersion != permissionVersion)
        {
            return UserSessionState.Invalid;
        }
        User? user = await users.FindByIdForAuthenticationAsync(userId, cancellationToken);
        if (user is null || !user.IsActive || user.IsLockedOut(clock.GetUtcNow().UtcDateTime) ||
            string.IsNullOrWhiteSpace(stamp) || !string.Equals(user.SecurityStamp, stamp, StringComparison.Ordinal))
        {
            return UserSessionState.Invalid;
        }
        return user.MustChangePassword ? UserSessionState.PasswordChangeRequired : UserSessionState.Valid;
    }
}

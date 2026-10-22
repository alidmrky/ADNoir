using AdNoir.BuildingBlocks.Context;
using AdNoir.Domain.Abstractions.Repositories;
using AdNoir.IdentityAndOrg.Application.Identity.Abstractions;
using AdNoir.IdentityAndOrg.Application.Identity.Dtos;
using AdNoir.IdentityAndOrg.Domain.Identity;
using AdNoir.Shared.Contracts.Permissions;

namespace AdNoir.IdentityAndOrg.Application.Identity;

/// <summary>Checks live actor state so stale claims cannot authorize new role grants.</summary>
public sealed class RoleGrantPolicy(ICurrentUser currentUser, IUserRepository users,
    IRoleRepository roles, ITenantContext tenant, TimeProvider clock) : IRoleGrantPolicy
{
    /// <inheritdoc />
    public async Task<bool> CanManageAsync(string code, IReadOnlyList<string> permissions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        // Platform powers belong only to immutable system roles, never custom tenant roles.
        if (string.Equals(code.Trim(), SystemRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(code.Trim(), SystemRoles.TenantAdmin, StringComparison.OrdinalIgnoreCase) ||
            permissions.Contains(PermissionCatalog.Tenants.Manage, StringComparer.Ordinal))
        {
            return false;
        }

        User? actor = await GetActorAsync(cancellationToken);
        if (actor is null || actor.EffectiveWriteScope != DataScope.Tenant ||
            currentUser.WriteScope != DataScope.Tenant)
        {
            return false;
        }
        IReadOnlyList<Role> available = await roles.ListForTenantAsync(tenant.TenantId, cancellationToken);
        HashSet<string> granted = PermissionsFor(actor, available);
        return granted.Contains(PermissionCatalog.Roles.Manage) && permissions.All(granted.Contains);
    }

    /// <inheritdoc />
    public async Task<bool> CanAssignAsync(User target, IReadOnlyList<RoleAssignmentDto> assignments,
        IReadOnlyList<Role> available, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(assignments);
        ArgumentNullException.ThrowIfNull(available);
        User? actor = await GetActorAsync(cancellationToken);
        if (actor is null || target.TenantId != tenant.TenantId)
        {
            return false;
        }
        HashSet<string> granted = PermissionsFor(actor, available);
        if (!granted.Contains(PermissionCatalog.Users.Manage))
        {
            return false;
        }
        bool platformAdministrator = available.Any(role =>
            role.IsSystemRole && role.TenantId is null && role.Code == SystemRoles.SuperAdmin &&
            actor.RoleAssignments.Any(assignment => assignment.RoleId == role.Id && assignment.RoleCode == role.Code));

        // Checking existing grants also prevents demoting an account the caller cannot administer.
        IEnumerable<RoleAssignmentDto> changes = assignments.Concat(target.RoleAssignments.Select(
            assignment => new RoleAssignmentDto(assignment.RoleId, assignment.RoleCode, assignment.ReadScope, assignment.WriteScope)));
        foreach (RoleAssignmentDto assignment in changes)
        {
            Role? role = available.FirstOrDefault(item => item.Id == assignment.RoleId);
            if (role is null ||
                (!platformAdministrator && (role.Code == SystemRoles.SuperAdmin ||
                    role.Permissions.Contains(PermissionCatalog.Tenants.Manage, StringComparer.Ordinal))) ||
                !role.Permissions.All(granted.Contains) ||
                assignment.ReadScope > actor.EffectiveReadScope || assignment.ReadScope > currentUser.ReadScope ||
                assignment.WriteScope > actor.EffectiveWriteScope || assignment.WriteScope > currentUser.WriteScope ||
                !Enum.IsDefined(assignment.ReadScope) || !Enum.IsDefined(assignment.WriteScope))
            {
                return false;
            }
        }
        return true;
    }

    private async Task<User?> GetActorAsync(CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || !tenant.HasTenant)
        {
            return null;
        }
        User? actor = await users.FindByIdForAuthenticationAsync(currentUser.UserId, cancellationToken);
        return actor is not null && actor.IsActive && !actor.IsLockedOut(clock.GetUtcNow().UtcDateTime) &&
            string.Equals(actor.SecurityStamp, currentUser.SecurityStamp, StringComparison.Ordinal)
            ? actor : null;
    }

    private static HashSet<string> PermissionsFor(User actor, IReadOnlyList<Role> available) =>
        available.Where(role => actor.RoleAssignments.Any(
                assignment => assignment.RoleId == role.Id && assignment.RoleCode == role.Code))
            .SelectMany(role => role.Permissions).ToHashSet(StringComparer.Ordinal);
}

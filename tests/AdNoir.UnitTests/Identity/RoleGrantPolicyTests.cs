using AdNoir.BuildingBlocks.Context;
using AdNoir.Domain.Abstractions.Repositories;
using AdNoir.IdentityAndOrg.Application.Identity;
using AdNoir.IdentityAndOrg.Application.Identity.Dtos;
using AdNoir.IdentityAndOrg.Domain.Identity;
using AdNoir.Shared.Contracts.Permissions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace AdNoir.UnitTests.Identity;

/// <summary>
/// The privilege-escalation guard: who may create a role, and who may hand one to a user.
/// Every case below is a way an actor could otherwise end up granting more than they hold.
/// </summary>
public sealed class RoleGrantPolicyTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherTenantId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly Guid ActorId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OrgUnitId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid AdminRoleId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid PlainRoleId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid SuperAdminRoleId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private static readonly DateTimeOffset Now = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private const string Stamp = "stamp-1";

    // ------------------------------------------------------------------ builders

    private static User Actor(
        DataScope write = DataScope.Tenant,
        DataScope read = DataScope.Tenant,
        params Guid[] roleIds)
    {
        User user = User.Create(TenantId, OrgUnitId, "actor@test", "Actor", "hash");
        user.Id = ActorId;
        user.SecurityStamp = Stamp;
        user.RoleAssignments = [.. roleIds.Select(id =>
            new RoleAssignment(id, id == AdminRoleId ? "Admin" : id == SuperAdminRoleId ? SystemRoles.SuperAdmin : "Plain", read, write))];
        return user;
    }

    /// <param name="global">
    /// A global system role carries no TenantId. The platform-administrator check requires
    /// exactly that, so it has to be expressible rather than defaulted to the tenant.
    /// </param>
    private static Role RoleOf(Guid id, string code, string[] permissions, bool system = false, bool global = false) =>
        new() { Id = id, Code = code, Name = code, Permissions = permissions, IsSystemRole = system, TenantId = global ? null : TenantId };

    private static User Target(Guid? tenantId = null, params RoleAssignment[] assignments)
    {
        User user = User.Create(tenantId ?? TenantId, OrgUnitId, "target@test", "Target", "hash");
        user.RoleAssignments = assignments;
        return user;
    }

    private sealed record Harness(
        RoleGrantPolicy Policy,
        ICurrentUser CurrentUser,
        IUserRepository Users,
        IRoleRepository Roles,
        ITenantContext Tenant);

    private static Harness Build(
        User? actor,
        IReadOnlyList<Role> available,
        DataScope callerWrite = DataScope.Tenant,
        DataScope callerRead = DataScope.Tenant,
        bool authenticated = true,
        bool hasTenant = true,
        string stamp = Stamp)
    {
        ICurrentUser currentUser = Substitute.For<ICurrentUser>();
        currentUser.IsAuthenticated.Returns(authenticated);
        currentUser.UserId.Returns(ActorId);
        currentUser.SecurityStamp.Returns(stamp);
        currentUser.WriteScope.Returns(callerWrite);
        currentUser.ReadScope.Returns(callerRead);

        ITenantContext tenant = Substitute.For<ITenantContext>();
        tenant.HasTenant.Returns(hasTenant);
        tenant.TenantId.Returns(TenantId);

        IUserRepository users = Substitute.For<IUserRepository>();
        users.FindByIdForAuthenticationAsync(ActorId, Arg.Any<CancellationToken>()).Returns(actor);

        IRoleRepository roles = Substitute.For<IRoleRepository>();
        roles.ListForTenantAsync(TenantId, Arg.Any<CancellationToken>()).Returns(available);

        return new Harness(
            new RoleGrantPolicy(currentUser, users, roles, tenant, new FixedClock()),
            currentUser, users, roles, tenant);
    }

    /// <summary>An actor holding role:manage and user:manage at tenant scope.</summary>
    private static Harness TenantAdminHarness(params Role[] extraRoles)
    {
        Role admin = RoleOf(AdminRoleId, "Admin",
            [PermissionCatalog.Roles.Manage, PermissionCatalog.Users.Manage, "item:read"]);

        return Build(Actor(roleIds: AdminRoleId), [admin, .. extraRoles]);
    }

    // ================================================================== CanManageAsync

    [Theory]
    [InlineData("SuperAdmin")]
    [InlineData("superadmin")]
    [InlineData("  SuperAdmin  ")]
    [InlineData("TenantAdmin")]
    [InlineData("tenantadmin")]
    public async Task A_custom_role_can_never_reuse_a_system_role_code(string code)
    {
        // Otherwise a tenant could mint its own "SuperAdmin" and inherit platform meaning.
        Harness h = TenantAdminHarness();

        (await h.Policy.CanManageAsync(code, [], CancellationToken.None)).ShouldBeFalse();
    }

    [Fact]
    public async Task A_custom_role_can_never_carry_the_platform_tenant_permission()
    {
        Harness h = TenantAdminHarness();

        (await h.Policy.CanManageAsync("Reports", [PermissionCatalog.Tenants.Manage], CancellationToken.None))
            .ShouldBeFalse();
    }

    [Fact]
    public async Task An_unauthenticated_caller_cannot_manage_roles()
    {
        Harness h = Build(Actor(roleIds: AdminRoleId),
            [RoleOf(AdminRoleId, "Admin", [PermissionCatalog.Roles.Manage])], authenticated: false);

        (await h.Policy.CanManageAsync("Reports", [], CancellationToken.None)).ShouldBeFalse();
    }

    [Fact]
    public async Task A_caller_without_a_resolved_tenant_cannot_manage_roles()
    {
        Harness h = Build(Actor(roleIds: AdminRoleId),
            [RoleOf(AdminRoleId, "Admin", [PermissionCatalog.Roles.Manage])], hasTenant: false);

        (await h.Policy.CanManageAsync("Reports", [], CancellationToken.None)).ShouldBeFalse();
    }

    [Fact]
    public async Task A_deleted_account_whose_token_still_works_cannot_manage_roles()
    {
        Harness h = Build(null, [RoleOf(AdminRoleId, "Admin", [PermissionCatalog.Roles.Manage])]);

        (await h.Policy.CanManageAsync("Reports", [], CancellationToken.None)).ShouldBeFalse();
    }

    [Fact]
    public async Task A_deactivated_account_cannot_manage_roles()
    {
        User actor = Actor(roleIds: AdminRoleId);
        actor.IsActive = false;

        Harness h = Build(actor, [RoleOf(AdminRoleId, "Admin", [PermissionCatalog.Roles.Manage])]);

        (await h.Policy.CanManageAsync("Reports", [], CancellationToken.None)).ShouldBeFalse();
    }

    [Fact]
    public async Task A_locked_out_account_cannot_manage_roles()
    {
        User actor = Actor(roleIds: AdminRoleId);
        actor.LockoutEndUtc = Now.UtcDateTime.AddMinutes(5);

        Harness h = Build(actor, [RoleOf(AdminRoleId, "Admin", [PermissionCatalog.Roles.Manage])]);

        (await h.Policy.CanManageAsync("Reports", [], CancellationToken.None)).ShouldBeFalse();
    }

    [Fact]
    public async Task A_stale_security_stamp_cannot_manage_roles()
    {
        // The token was issued before a permission or password change; it must not act.
        Harness h = Build(Actor(roleIds: AdminRoleId),
            [RoleOf(AdminRoleId, "Admin", [PermissionCatalog.Roles.Manage])], stamp: "stamp-OLD");

        (await h.Policy.CanManageAsync("Reports", [], CancellationToken.None)).ShouldBeFalse();
    }

    [Theory]
    [InlineData(DataScope.Self)]
    [InlineData(DataScope.Unit)]
    [InlineData(DataScope.Subtree)]
    public async Task Managing_roles_requires_tenant_wide_write_on_the_stored_assignment(DataScope scope)
    {
        Harness h = Build(Actor(write: scope, roleIds: AdminRoleId),
            [RoleOf(AdminRoleId, "Admin", [PermissionCatalog.Roles.Manage])]);

        (await h.Policy.CanManageAsync("Reports", [], CancellationToken.None)).ShouldBeFalse();
    }

    [Theory]
    [InlineData(DataScope.Self)]
    [InlineData(DataScope.Unit)]
    [InlineData(DataScope.Subtree)]
    public async Task Managing_roles_requires_tenant_wide_write_on_the_token_too(DataScope scope)
    {
        // Both are checked: the stored assignment can be current while the token is narrower.
        Harness h = Build(Actor(roleIds: AdminRoleId),
            [RoleOf(AdminRoleId, "Admin", [PermissionCatalog.Roles.Manage])], callerWrite: scope);

        (await h.Policy.CanManageAsync("Reports", [], CancellationToken.None)).ShouldBeFalse();
    }

    [Fact]
    public async Task An_actor_without_role_manage_cannot_create_roles()
    {
        Harness h = Build(Actor(roleIds: PlainRoleId), [RoleOf(PlainRoleId, "Plain", ["item:read"])]);

        (await h.Policy.CanManageAsync("Reports", ["item:read"], CancellationToken.None)).ShouldBeFalse();
    }

    [Fact]
    public async Task An_actor_cannot_put_a_permission_they_do_not_hold_into_a_new_role()
    {
        // The core escalation: granting yourself more by authoring a role that has it.
        Harness h = TenantAdminHarness();

        (await h.Policy.CanManageAsync("Reports", ["item:delete"], CancellationToken.None)).ShouldBeFalse();
    }

    [Fact]
    public async Task An_actor_may_create_a_role_from_permissions_they_already_hold()
    {
        Harness h = TenantAdminHarness();

        (await h.Policy.CanManageAsync("Reports", ["item:read"], CancellationToken.None)).ShouldBeTrue();
    }

    [Fact]
    public async Task A_role_with_no_permissions_is_allowed_for_a_role_manager()
    {
        Harness h = TenantAdminHarness();

        (await h.Policy.CanManageAsync("Reports", [], CancellationToken.None)).ShouldBeTrue();
    }

    // ================================================================== CanAssignAsync

    private static Role AdminRole() => RoleOf(AdminRoleId, "Admin",
        [PermissionCatalog.Roles.Manage, PermissionCatalog.Users.Manage, "item:read"]);

    [Fact]
    public async Task Assigning_to_a_user_in_another_tenant_is_refused()
    {
        Harness h = TenantAdminHarness();

        (await h.Policy.CanAssignAsync(Target(OtherTenantId), [], [AdminRole()], CancellationToken.None))
            .ShouldBeFalse();
    }

    [Fact]
    public async Task An_actor_without_user_manage_cannot_assign_roles()
    {
        Harness h = Build(Actor(roleIds: PlainRoleId), [RoleOf(PlainRoleId, "Plain", ["item:read"])]);

        (await h.Policy.CanAssignAsync(Target(), [], [RoleOf(PlainRoleId, "Plain", ["item:read"])],
            CancellationToken.None)).ShouldBeFalse();
    }

    [Fact]
    public async Task A_tenant_administrator_cannot_hand_out_SuperAdmin()
    {
        // The headline rule: only an existing platform administrator may create another.
        //
        // The SuperAdmin role deliberately carries a permission the actor already holds, so
        // the permission-subset check cannot be what rejects this. The role code is the only
        // thing standing in the way - which is exactly the rule under test.
        Role superAdmin = RoleOf(SuperAdminRoleId, SystemRoles.SuperAdmin, ["item:read"],
            system: true, global: true);
        Harness h = Build(Actor(roleIds: AdminRoleId), [AdminRole(), superAdmin]);

        RoleAssignmentDto assignment =
            new(SuperAdminRoleId, SystemRoles.SuperAdmin, DataScope.Unit, DataScope.Unit);

        (await h.Policy.CanAssignAsync(Target(), [assignment], [AdminRole(), superAdmin],
            CancellationToken.None)).ShouldBeFalse();
    }

    [Fact]
    public async Task A_tenant_administrator_cannot_hand_out_a_role_carrying_tenant_manage()
    {
        // The actor holds tenant:manage here, so the subset check passes and only the
        // platform-permission guard can refuse the assignment.
        Role actorRole = RoleOf(AdminRoleId, "Admin",
            [PermissionCatalog.Roles.Manage, PermissionCatalog.Users.Manage, PermissionCatalog.Tenants.Manage]);
        Role platform = RoleOf(PlainRoleId, "Platform", [PermissionCatalog.Tenants.Manage]);
        Harness h = Build(Actor(roleIds: AdminRoleId), [actorRole, platform]);

        RoleAssignmentDto assignment = new(PlainRoleId, "Platform", DataScope.Unit, DataScope.Unit);

        (await h.Policy.CanAssignAsync(Target(), [assignment], [actorRole, platform],
            CancellationToken.None)).ShouldBeFalse();
    }

    [Fact]
    public async Task An_unknown_role_id_is_refused()
    {
        Harness h = TenantAdminHarness();
        RoleAssignmentDto assignment = new(Guid.NewGuid(), "Ghost", DataScope.Unit, DataScope.Unit);

        (await h.Policy.CanAssignAsync(Target(), [assignment], [AdminRole()], CancellationToken.None))
            .ShouldBeFalse();
    }

    [Fact]
    public async Task A_role_whose_permissions_exceed_the_actors_own_is_refused()
    {
        Role richer = RoleOf(PlainRoleId, "Richer", ["item:read", "item:delete"]);
        Harness h = Build(Actor(roleIds: AdminRoleId), [AdminRole(), richer]);

        RoleAssignmentDto assignment = new(PlainRoleId, "Richer", DataScope.Unit, DataScope.Unit);

        (await h.Policy.CanAssignAsync(Target(), [assignment], [AdminRole(), richer],
            CancellationToken.None)).ShouldBeFalse();
    }

    [Fact]
    public async Task A_scope_wider_than_the_actors_own_is_refused()
    {
        Role plain = RoleOf(PlainRoleId, "Plain", ["item:read"]);
        Harness h = Build(Actor(write: DataScope.Unit, read: DataScope.Unit, roleIds: AdminRoleId),
            [AdminRole(), plain], callerWrite: DataScope.Unit, callerRead: DataScope.Unit);

        RoleAssignmentDto assignment = new(PlainRoleId, "Plain", DataScope.Tenant, DataScope.Unit);

        (await h.Policy.CanAssignAsync(Target(), [assignment], [AdminRole(), plain],
            CancellationToken.None)).ShouldBeFalse();
    }

    [Fact]
    public async Task A_scope_wider_than_the_token_is_refused_even_when_the_stored_role_allows_it()
    {
        Role plain = RoleOf(PlainRoleId, "Plain", ["item:read"]);
        Harness h = Build(Actor(roleIds: AdminRoleId), [AdminRole(), plain], callerWrite: DataScope.Unit);

        RoleAssignmentDto assignment = new(PlainRoleId, "Plain", DataScope.Unit, DataScope.Tenant);

        (await h.Policy.CanAssignAsync(Target(), [assignment], [AdminRole(), plain],
            CancellationToken.None)).ShouldBeFalse();
    }

    [Fact]
    public async Task A_scope_value_outside_the_enum_is_refused()
    {
        // A crafted payload must not slip past the comparison by carrying an undefined value.
        Role plain = RoleOf(PlainRoleId, "Plain", ["item:read"]);
        Harness h = Build(Actor(roleIds: AdminRoleId), [AdminRole(), plain]);

        RoleAssignmentDto assignment = new(PlainRoleId, "Plain", (DataScope)42, DataScope.Unit);

        (await h.Policy.CanAssignAsync(Target(), [assignment], [AdminRole(), plain],
            CancellationToken.None)).ShouldBeFalse();
    }

    [Fact]
    public async Task A_target_already_holding_a_role_the_actor_cannot_administer_is_refused()
    {
        // Without checking the existing grants, a tenant admin could demote a SuperAdmin by
        // replacing their assignments with a harmless-looking list. The role's permission is
        // one the actor holds, so only the SuperAdmin code can be what refuses this.
        Role superAdmin = RoleOf(SuperAdminRoleId, SystemRoles.SuperAdmin, ["item:read"],
            system: true, global: true);
        Harness h = Build(Actor(roleIds: AdminRoleId), [AdminRole(), superAdmin]);

        User target = Target(TenantId,
            new RoleAssignment(SuperAdminRoleId, SystemRoles.SuperAdmin, DataScope.Unit, DataScope.Unit));

        (await h.Policy.CanAssignAsync(target, [], [AdminRole(), superAdmin], CancellationToken.None))
            .ShouldBeFalse();
    }

    [Fact]
    public async Task A_platform_administrator_may_hand_out_SuperAdmin()
    {
        Role superAdmin = RoleOf(SuperAdminRoleId, SystemRoles.SuperAdmin,
            [PermissionCatalog.Users.Manage], system: true, global: true);

        Harness h = Build(Actor(roleIds: SuperAdminRoleId), [superAdmin]);

        RoleAssignmentDto assignment =
            new(SuperAdminRoleId, SystemRoles.SuperAdmin, DataScope.Tenant, DataScope.Tenant);

        (await h.Policy.CanAssignAsync(Target(), [assignment], [superAdmin], CancellationToken.None))
            .ShouldBeTrue();
    }

    [Fact]
    public async Task A_valid_assignment_within_the_actors_own_reach_is_allowed()
    {
        Role plain = RoleOf(PlainRoleId, "Plain", ["item:read"]);
        Harness h = Build(Actor(roleIds: AdminRoleId), [AdminRole(), plain]);

        RoleAssignmentDto assignment = new(PlainRoleId, "Plain", DataScope.Unit, DataScope.Unit);

        (await h.Policy.CanAssignAsync(Target(), [assignment], [AdminRole(), plain],
            CancellationToken.None)).ShouldBeTrue();
    }

    [Fact]
    public async Task Clearing_every_assignment_is_allowed_when_the_target_holds_nothing_special()
    {
        Harness h = TenantAdminHarness();

        (await h.Policy.CanAssignAsync(Target(), [], [AdminRole()], CancellationToken.None)).ShouldBeTrue();
    }

    [Fact]
    public async Task Null_arguments_are_rejected_rather_than_treated_as_empty()
    {
        Harness h = TenantAdminHarness();

        await Should.ThrowAsync<ArgumentNullException>(() =>
            h.Policy.CanAssignAsync(null!, [], [AdminRole()], CancellationToken.None));
        await Should.ThrowAsync<ArgumentNullException>(() =>
            h.Policy.CanAssignAsync(Target(), null!, [AdminRole()], CancellationToken.None));
        await Should.ThrowAsync<ArgumentNullException>(() =>
            h.Policy.CanAssignAsync(Target(), [], null!, CancellationToken.None));
        await Should.ThrowAsync<ArgumentNullException>(() =>
            h.Policy.CanManageAsync("Reports", null!, CancellationToken.None));
    }
}

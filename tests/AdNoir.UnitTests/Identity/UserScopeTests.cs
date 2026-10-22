using AdNoir.Domain.Abstractions.Repositories;
using AdNoir.IdentityAndOrg.Domain.Identity;
using Shouldly;
using Xunit;

namespace AdNoir.UnitTests.Identity;

/// <summary>
/// How much data a user may reach. The repository turns the effective scope into a query
/// filter, so widening it by accident is a data leak and narrowing it is a bug report that
/// reads "records are missing sometimes".
/// </summary>
public sealed class UserScopeTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OrgUnitId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static User WithRoles(params RoleAssignment[] assignments)
    {
        User user = User.Create(TenantId, OrgUnitId, "a@b.test", "Tester", "hash");
        user.RoleAssignments = assignments;
        return user;
    }

    private static RoleAssignment Role(DataScope read, DataScope write) =>
        new(Guid.NewGuid(), "role", read, write);

    [Fact]
    public void A_user_with_no_roles_reaches_only_their_own_records()
    {
        // The widest-scope fold starts at Self, so an unassigned account must not fall through
        // to something broader.
        User user = WithRoles();

        user.EffectiveReadScope.ShouldBe(DataScope.Self);
        user.EffectiveWriteScope.ShouldBe(DataScope.Self);
    }

    [Theory]
    [InlineData(DataScope.Self)]
    [InlineData(DataScope.Unit)]
    [InlineData(DataScope.Subtree)]
    [InlineData(DataScope.Tenant)]
    public void A_single_role_decides_the_effective_scope(DataScope scope)
    {
        User user = WithRoles(Role(scope, scope));

        user.EffectiveReadScope.ShouldBe(scope);
        user.EffectiveWriteScope.ShouldBe(scope);
    }

    [Fact]
    public void Two_roles_combine_to_the_more_permissive_one()
    {
        // Holding both roles must not somehow be narrower than holding either alone.
        User user = WithRoles(
            Role(DataScope.Unit, DataScope.Self),
            Role(DataScope.Subtree, DataScope.Unit));

        user.EffectiveReadScope.ShouldBe(DataScope.Subtree);
        user.EffectiveWriteScope.ShouldBe(DataScope.Unit);
    }

    [Fact]
    public void Role_order_does_not_change_the_result()
    {
        RoleAssignment narrow = Role(DataScope.Self, DataScope.Self);
        RoleAssignment wide = Role(DataScope.Tenant, DataScope.Tenant);

        WithRoles(narrow, wide).EffectiveReadScope
            .ShouldBe(WithRoles(wide, narrow).EffectiveReadScope);
    }

    [Fact]
    public void Read_and_write_scopes_are_independent()
    {
        // The common pairing is read across the subtree but write only within the unit; a fold
        // that mixed the two would quietly grant subtree writes.
        User user = WithRoles(Role(DataScope.Subtree, DataScope.Unit));

        user.EffectiveReadScope.ShouldBe(DataScope.Subtree);
        user.EffectiveWriteScope.ShouldBe(DataScope.Unit);
    }

    [Fact]
    public void Role_codes_are_exposed_for_the_token() =>
        WithRoles(new RoleAssignment(Guid.NewGuid(), "admin"), new RoleAssignment(Guid.NewGuid(), "viewer"))
            .RoleCodes.ShouldBe(["admin", "viewer"]);

    // ------------------------------------------------------------------ lockout

    [Fact]
    public void An_account_with_no_deadline_is_not_locked_out() =>
        WithRoles().IsLockedOut(DateTime.UtcNow).ShouldBeFalse();

    [Fact]
    public void An_account_is_locked_out_while_the_deadline_is_in_the_future()
    {
        DateTime now = new(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);
        User user = WithRoles();
        user.LockoutEndUtc = now.AddMinutes(1);

        user.IsLockedOut(now).ShouldBeTrue();
    }

    [Fact]
    public void An_expired_lockout_no_longer_blocks_sign_in()
    {
        DateTime now = new(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);
        User user = WithRoles();
        user.LockoutEndUtc = now.AddMinutes(-1);

        user.IsLockedOut(now).ShouldBeFalse();
    }

    [Fact]
    public void A_lockout_expiring_exactly_now_is_over()
    {
        DateTime now = new(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);
        User user = WithRoles();
        user.LockoutEndUtc = now;

        user.IsLockedOut(now).ShouldBeFalse();
    }

    // ------------------------------------------------------------------ identity

    [Theory]
    [InlineData("User@Example.COM", "USER@EXAMPLE.COM")]
    [InlineData("  user@example.com  ", "USER@EXAMPLE.COM")]
    public void Normalising_an_address_makes_casing_and_padding_irrelevant(string input, string expected)
    {
        // Two accounts a human would read as the same address must collide on the unique index.
        User.NormalizeEmail(input).ShouldBe(expected);
    }

    [Fact]
    public void Creating_a_user_stores_the_address_as_entered_and_normalised()
    {
        User user = User.Create(TenantId, OrgUnitId, "  User@Example.com  ", "  Tester  ", "hash");

        user.Email.ShouldBe("User@Example.com");
        user.NormalizedEmail.ShouldBe("USER@EXAMPLE.COM");
        user.DisplayName.ShouldBe("Tester");
        user.TenantId.ShouldBe(TenantId);
        user.OrgUnitId.ShouldBe(OrgUnitId);
        user.IsActive.ShouldBeTrue();
    }

    [Theory]
    [InlineData("", "name", "hash")]
    [InlineData("  ", "name", "hash")]
    [InlineData("a@b.test", "", "hash")]
    [InlineData("a@b.test", "name", "")]
    public void Creating_a_user_without_the_required_fields_is_rejected(string email, string name, string hash) =>
        Should.Throw<ArgumentException>(() => User.Create(TenantId, OrgUnitId, email, name, hash));

    [Fact]
    public void A_new_user_starts_with_a_security_stamp() =>
        User.Create(TenantId, OrgUnitId, "a@b.test", "Tester", "hash").SecurityStamp.ShouldNotBeNullOrWhiteSpace();

    [Fact]
    public void Rotating_the_stamp_invalidates_what_was_issued_before_it()
    {
        // Every live-session check compares this value, so rotation is how a permission or
        // password change reaches services that already hold a token.
        User user = WithRoles();
        string before = user.SecurityStamp;

        user.RotateSecurityStamp();

        user.SecurityStamp.ShouldNotBe(before);
    }

    [Fact]
    public void Each_generated_stamp_is_distinct() =>
        User.NewSecurityStamp().ShouldNotBe(User.NewSecurityStamp());
}

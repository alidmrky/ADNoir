using AdNoir.IdentityAndOrg.Domain.Identity;
using Shouldly;
using Xunit;

namespace AdNoir.UnitTests.Identity;

/// <summary>
/// Brute-force protection. The gateway's per-IP quota does not cover a distributed attempt
/// against one account, so this is the only thing standing in its way.
/// </summary>
public sealed class LockoutPolicyTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    public void Attempts_below_the_threshold_do_not_lock_the_account(int failures) =>
        LockoutPolicy.ComputeLockout(failures).ShouldBeNull();

    [Fact]
    public void The_threshold_attempt_triggers_the_initial_lockout() =>
        LockoutPolicy.ComputeLockout(LockoutPolicy.MaxFailedAttempts)
            .ShouldBe(LockoutPolicy.InitialLockout);

    [Fact]
    public void Each_further_failure_doubles_the_lockout()
    {
        double first = LockoutPolicy.ComputeLockout(LockoutPolicy.MaxFailedAttempts)!.Value.TotalMinutes;
        double second = LockoutPolicy.ComputeLockout(LockoutPolicy.MaxFailedAttempts + 1)!.Value.TotalMinutes;
        double third = LockoutPolicy.ComputeLockout(LockoutPolicy.MaxFailedAttempts + 2)!.Value.TotalMinutes;

        // A fixed delay is trivial to wait out in a script; doubling is what makes sustained
        // guessing pointless.
        second.ShouldBe(first * 2);
        third.ShouldBe(second * 2);
    }

    [Fact]
    public void The_lockout_is_capped_so_an_account_is_never_locked_out_permanently() =>
        LockoutPolicy.ComputeLockout(LockoutPolicy.MaxFailedAttempts + 50)
            .ShouldBe(LockoutPolicy.MaximumLockout);

    [Fact]
    public void An_absurd_failure_count_does_not_overflow_the_multiplier()
    {
        // The shift is capped before it is applied; without that, a large count wraps negative
        // and the lockout silently disappears.
        LockoutPolicy.ComputeLockout(int.MaxValue).ShouldBe(LockoutPolicy.MaximumLockout);
    }

    [Fact]
    public void The_lockout_never_exceeds_the_maximum_at_any_failure_count()
    {
        for (int failures = LockoutPolicy.MaxFailedAttempts; failures < LockoutPolicy.MaxFailedAttempts + 40; failures++)
        {
            LockoutPolicy.ComputeLockout(failures)!.Value
                .ShouldBeLessThanOrEqualTo(LockoutPolicy.MaximumLockout, $"at {failures} failures");
        }
    }

    [Fact]
    public void Registering_a_failure_below_the_threshold_only_increments_the_counter()
    {
        DateTime now = new(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);

        (int failedCount, DateTime? lockoutEnd) = LockoutPolicy.RegisterFailure(0, now);

        failedCount.ShouldBe(1);
        lockoutEnd.ShouldBeNull();
    }

    [Fact]
    public void Registering_the_threshold_failure_sets_a_deadline_from_the_supplied_clock()
    {
        DateTime now = new(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);

        (int failedCount, DateTime? lockoutEnd) =
            LockoutPolicy.RegisterFailure(LockoutPolicy.MaxFailedAttempts - 1, now);

        failedCount.ShouldBe(LockoutPolicy.MaxFailedAttempts);
        lockoutEnd.ShouldBe(now.Add(LockoutPolicy.InitialLockout));
    }

    [Fact]
    public void Consecutive_failures_push_the_deadline_further_out()
    {
        DateTime now = new(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);

        DateTime atThreshold = LockoutPolicy.RegisterFailure(LockoutPolicy.MaxFailedAttempts - 1, now).LockoutEndUtc!.Value;
        DateTime oneLater = LockoutPolicy.RegisterFailure(LockoutPolicy.MaxFailedAttempts, now).LockoutEndUtc!.Value;

        oneLater.ShouldBeGreaterThan(atThreshold);
    }
}

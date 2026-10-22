namespace AdNoir.IdentityAndOrg.Domain.Identity;

/// <summary>
/// Brute-force protection.
/// </summary>
/// <remarks>
/// <para>
/// Because ASP.NET Core Identity's store surface is not used, lockout is our own
/// responsibility and cannot be skipped. The gateway's IP-based rate limiting does not cover
/// this case: a distributed attempt spreads across addresses and never trips a per-IP limit,
/// while still hammering one account.
/// </para>
/// <para>
/// Pure functions with no dependencies, so the policy can be reasoned about - and changed -
/// without touching the sign-in flow.
/// </para>
/// </remarks>
public static class LockoutPolicy
{
    /// <summary>Failed attempts tolerated before the first lockout.</summary>
    public const int MaxFailedAttempts = 5;

    /// <summary>Duration of the first lockout.</summary>
    public static readonly TimeSpan InitialLockout = TimeSpan.FromMinutes(1);

    /// <summary>Upper bound, so an abandoned account is not locked out permanently.</summary>
    public static readonly TimeSpan MaximumLockout = TimeSpan.FromHours(1);

    /// <summary>
    /// The lockout to apply after a failed attempt, or null when the threshold has not been
    /// reached yet.
    /// </summary>
    /// <param name="accessFailedCount">Consecutive failures including the one just recorded.</param>
    /// <returns>How long to lock the account out, or null.</returns>
    /// <remarks>
    /// Doubling from one minute: the fifth failure costs a minute, the sixth two, and so on up
    /// to <see cref="MaximumLockout"/>. A fixed delay is trivial to wait out in a script; a
    /// doubling one makes sustained guessing pointless while barely inconveniencing a person
    /// who mistyped their password twice.
    /// </remarks>
    public static TimeSpan? ComputeLockout(int accessFailedCount)
    {
        if (accessFailedCount < MaxFailedAttempts)
        {
            return null;
        }

        int step = accessFailedCount - MaxFailedAttempts;

        // Capped before the shift so the multiplier cannot overflow on an absurd failure count.
        int multiplier = 1 << Math.Min(step, 20);

        double minutes = InitialLockout.TotalMinutes * multiplier;

        return minutes >= MaximumLockout.TotalMinutes
            ? MaximumLockout
            : TimeSpan.FromMinutes(minutes);
    }

    /// <summary>
    /// Records a failed attempt and returns the new counter and lockout deadline.
    /// </summary>
    /// <param name="currentFailedCount">The counter before this attempt.</param>
    /// <param name="utcNow">The current time, in UTC.</param>
    /// <returns>The updated counter and the lockout end, when one applies.</returns>
    public static (int FailedCount, DateTime? LockoutEndUtc) RegisterFailure(
        int currentFailedCount,
        DateTime utcNow)
    {
        int failedCount = currentFailedCount + 1;
        TimeSpan? lockout = ComputeLockout(failedCount);

        return (failedCount, lockout is TimeSpan duration ? utcNow.Add(duration) : null);
    }
}

using AdNoir.Domain.Abstractions.Entities;
using AdNoir.Domain.Abstractions.Entities.Bases;
using AdNoir.Domain.Abstractions.Repositories;

namespace AdNoir.IdentityAndOrg.Domain.Identity;

/// <summary>
/// A role granted to a user, together with how much data that role may reach.
/// </summary>
/// <param name="RoleId">The role.</param>
/// <param name="RoleCode">The role's code, denormalised so token issuing needs no join.</param>
/// <param name="ReadScope">How much the user may read through this role.</param>
/// <param name="WriteScope">How much the user may change through this role.</param>
/// <remarks>
/// Scopes sit on the assignment rather than on the role: the same <c>Manager</c> role means
/// "this unit" for one person and "this subtree" for another. The typical pairing is
/// read at <see cref="DataScope.Subtree"/> and write at <see cref="DataScope.Unit"/>.
/// </remarks>
public sealed record RoleAssignment(
    Guid RoleId,
    string RoleCode,
    DataScope ReadScope = DataScope.Unit,
    DataScope WriteScope = DataScope.Unit);

/// <summary>
/// A user account.
/// </summary>
/// <remarks>
/// ASP.NET Core Identity's store surface is not used; only its <c>PasswordHasher</c>. That
/// makes lockout our own responsibility, and it cannot be skipped: the gateway's IP-based
/// rate limiting does nothing against a distributed attempt.
/// </remarks>
public sealed class User : FullAuditedTenantEntity, IAuditable, IOrgUnitScoped
{
    /// <summary>E-mail address as entered.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Upper-cased e-mail used for lookups and uniqueness, so that casing differences cannot
    /// produce two accounts that a human would read as the same address.
    /// </summary>
    public string NormalizedEmail { get; set; } = string.Empty;

    /// <summary>Display name.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Hashed password. Never logged, never audited, never returned.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// Changes whenever the user's authorization changes. A token carrying a stale stamp is
    /// reissued with fresh claims on refresh, which is how a permission change reaches every
    /// service within one access-token lifetime.
    /// </summary>
    public string SecurityStamp { get; set; } = NewSecurityStamp();

    /// <inheritdoc />
    public Guid OrgUnitId { get; set; }

    /// <summary>Roles granted to this user, with their scopes.</summary>
    public IReadOnlyList<RoleAssignment> RoleAssignments { get; set; } = [];

    /// <summary>Whether the account may be used at all.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Whether the e-mail address has been confirmed.</summary>
    public bool EmailConfirmed { get; set; }

    /// <summary>
    /// Forces a password change at next sign-in. Set by the migrator when it generates the
    /// first administrator password.
    /// </summary>
    public bool MustChangePassword { get; set; }

    /// <summary>Consecutive failed sign-in attempts. Reset on success.</summary>
    public int AccessFailedCount { get; set; }

    /// <summary>When the current lockout expires, or null when not locked out.</summary>
    public DateTime? LockoutEndUtc { get; set; }

    /// <summary>When the user last signed in successfully.</summary>
    public DateTime? LastLoginAtUtc { get; set; }

    /// <summary>Whether the account is locked out at the given moment.</summary>
    /// <param name="utcNow">The current time, in UTC.</param>
    /// <returns><see langword="true"/> when sign-in must be refused.</returns>
    public bool IsLockedOut(DateTime utcNow) => LockoutEndUtc is DateTime end && end > utcNow;

    /// <summary>The role codes carried into the token.</summary>
    public IReadOnlyList<string> RoleCodes => [.. RoleAssignments.Select(assignment => assignment.RoleCode)];

    /// <summary>
    /// The widest read scope across the user's assignments, since a user holding two roles can
    /// read as far as the more permissive one allows.
    /// </summary>
    public DataScope EffectiveReadScope => WidestScope(RoleAssignments.Select(a => a.ReadScope));

    /// <summary>The widest write scope across the user's assignments.</summary>
    public DataScope EffectiveWriteScope => WidestScope(RoleAssignments.Select(a => a.WriteScope));

    /// <summary>Creates a user.</summary>
    /// <param name="tenantId">The owning tenant.</param>
    /// <param name="orgUnitId">The user's organisation unit.</param>
    /// <param name="email">E-mail address.</param>
    /// <param name="displayName">Display name.</param>
    /// <param name="passwordHash">Already-hashed password.</param>
    /// <returns>The new user.</returns>
    public static User Create(
        Guid tenantId,
        Guid orgUnitId,
        string email,
        string displayName,
        string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        return new User
        {
            TenantId = tenantId,
            OrgUnitId = orgUnitId,
            Email = email.Trim(),
            NormalizedEmail = NormalizeEmail(email),
            DisplayName = displayName.Trim(),
            PasswordHash = passwordHash,
        };
    }

    /// <summary>Normalises an e-mail address for lookup and uniqueness.</summary>
    /// <param name="email">The address.</param>
    /// <returns>The upper-cased, trimmed address.</returns>
    public static string NormalizeEmail(string email) =>
        email.Trim().ToUpperInvariant();

    /// <summary>Generates a security stamp.</summary>
    /// <returns>A new stamp.</returns>
    public static string NewSecurityStamp() => Guid.CreateVersion7().ToString("N");

    /// <summary>
    /// Rotates the security stamp. Called whenever authorization changes, and also on refresh
    /// token reuse, where it invalidates everything issued to the account.
    /// </summary>
    public void RotateSecurityStamp() => SecurityStamp = NewSecurityStamp();

    private static DataScope WidestScope(IEnumerable<DataScope> scopes)
    {
        DataScope widest = DataScope.Self;

        foreach (DataScope scope in scopes)
        {
            if (scope > widest)
            {
                widest = scope;
            }
        }

        return widest;
    }
}

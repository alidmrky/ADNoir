using AdNoir.IdentityAndOrg.Application.Identity.Abstractions;
using AdNoir.IdentityAndOrg.Domain.Identity;
using Microsoft.AspNetCore.Identity;

namespace AdNoir.IdentityAndOrg.Infrastructure.Security;

/// <summary>
/// Password hashing over ASP.NET Core Identity's <see cref="PasswordHasher{TUser}"/>.
/// </summary>
/// <remarks>
/// This class is the entire extent of our use of ASP.NET Core Identity. Its store surface -
/// <c>UserManager</c>, <c>IUserStore</c> and the rest - is deliberately avoided,
/// but there is no reason to reimplement a well-reviewed PBKDF2 hasher with sensible defaults
/// and a built-in rehash signal.
/// </remarks>
public sealed class IdentityPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<User> _hasher = new();
    private readonly User _dummyUser = new();
    private readonly string _dummyHash;

    /// <summary>Initializes a new instance.</summary>
    public IdentityPasswordHasher()
    {
        // Computed once so the dummy verification below costs the same as a real one without
        // paying for a hash on every failed sign-in.
        _dummyHash = _hasher.HashPassword(_dummyUser, "not-a-real-password-used-only-for-timing");
    }

    /// <inheritdoc />
    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        return _hasher.HashPassword(_dummyUser, password);
    }

    /// <inheritdoc />
    public PasswordVerificationOutcome Verify(string hash, string password)
    {
        if (string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(password))
        {
            return PasswordVerificationOutcome.Failed;
        }

        return _hasher.VerifyHashedPassword(_dummyUser, hash, password) switch
        {
            PasswordVerificationResult.Success => PasswordVerificationOutcome.Success,
            PasswordVerificationResult.SuccessRehashNeeded => PasswordVerificationOutcome.SuccessRehashNeeded,
            _ => PasswordVerificationOutcome.Failed,
        };
    }

    /// <inheritdoc />
    public void PerformDummyVerification() =>
        _hasher.VerifyHashedPassword(_dummyUser, _dummyHash, "any-input-the-result-is-discarded");
}

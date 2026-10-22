namespace AdNoir.IdentityAndOrg.Application.Identity.Abstractions;

/// <summary>The outcome of verifying a password.</summary>
public enum PasswordVerificationOutcome
{
    /// <summary>The password does not match.</summary>
    Failed = 0,

    /// <summary>The password matches.</summary>
    Success = 1,

    /// <summary>
    /// The password matches but was hashed with outdated parameters, so the stored hash should
    /// be replaced while the plaintext is available.
    /// </summary>
    SuccessRehashNeeded = 2,
}

/// <summary>
/// Hashes and verifies passwords.
/// </summary>
/// <remarks>
/// Declared here so the application layer never references an implementation. The
/// implementation wraps ASP.NET Core Identity's <c>PasswordHasher</c> - the only part of
/// Identity this system uses; its store surface is deliberately avoided.
/// </remarks>
public interface IPasswordHasher
{
    /// <summary>Hashes a password.</summary>
    /// <param name="password">The plaintext password.</param>
    /// <returns>The hash to store.</returns>
    string Hash(string password);

    /// <summary>Verifies a password against a stored hash.</summary>
    /// <param name="hash">The stored hash.</param>
    /// <param name="password">The plaintext password.</param>
    /// <returns>The verification outcome.</returns>
    PasswordVerificationOutcome Verify(string hash, string password);

    /// <summary>
    /// Performs a verification against a throwaway hash and discards the result.
    /// </summary>
    /// <remarks>
    /// Called when sign-in is attempted for an address that does not exist. Without it, a
    /// missing account returns noticeably faster than a wrong password, and that timing
    /// difference is enough to enumerate which addresses are registered.
    /// </remarks>
    void PerformDummyVerification();
}

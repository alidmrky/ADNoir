using System.Security.Cryptography;
using System.Text;
using AdNoir.Domain.Abstractions.Entities;
using AdNoir.Domain.Abstractions.Entities.Bases;

namespace AdNoir.IdentityAndOrg.Domain.Identity;

/// <summary>Why a refresh token was revoked.</summary>
public enum RefreshTokenRevocationReason
{
    /// <summary>Not revoked.</summary>
    None = 0,

    /// <summary>Rotated during a normal refresh.</summary>
    Rotated = 1,

    /// <summary>The user signed out.</summary>
    SignedOut = 2,

    /// <summary>An already-used token was presented again; the whole family was revoked.</summary>
    ReuseDetected = 3,

    /// <summary>Revoked because authorization changed.</summary>
    SecurityStampChanged = 4,
}

/// <summary>
/// A refresh token record.
/// </summary>
/// <remarks>
/// <para>
/// The token itself is never stored. Only its SHA-256 hash goes to the database, so a leaked
/// backup does not hand over working credentials.
/// </para>
/// <para>
/// Every token belongs to a <see cref="FamilyId"/> chain. A normal refresh revokes the
/// presented token and issues the next one in the same family. If a revoked token is ever
/// presented again, that is the signature of a stolen token being replayed: the
/// <strong>entire family</strong> is revoked and the user's security stamp is rotated.
/// Rejecting only the replayed token would leave the thief's copy working.
/// </para>
/// </remarks>
public sealed class RefreshToken : Entity, IMultiTenant
{
    /// <inheritdoc />
    public Guid TenantId { get; set; }

    /// <summary>The user this token was issued to.</summary>
    public Guid UserId { get; set; }

    /// <summary>SHA-256 hash of the token, hex-encoded.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>The rotation chain this token belongs to.</summary>
    public Guid FamilyId { get; set; }

    /// <summary>Position in the chain, starting at one. Useful when reading an incident trail.</summary>
    public int SequenceNumber { get; set; }

    /// <summary>When the token was issued.</summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>When the token expires. Also the anchor for the TTL index.</summary>
    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>When the token was revoked, or null while it is still usable.</summary>
    public DateTime? RevokedAtUtc { get; set; }

    /// <summary>Why the token was revoked.</summary>
    public RefreshTokenRevocationReason RevocationReason { get; set; }

    /// <summary>Hash of the token that replaced this one during rotation.</summary>
    public string? ReplacedByTokenHash { get; set; }

    /// <summary>IP the token was issued to, for incident analysis.</summary>
    public string? CreatedByIp { get; set; }

    /// <summary>Whether the token can still be exchanged.</summary>
    /// <param name="utcNow">The current time, in UTC.</param>
    /// <returns><see langword="true"/> when it is neither revoked nor expired.</returns>
    public bool IsUsable(DateTime utcNow) => RevokedAtUtc is null && ExpiresAtUtc > utcNow;

    /// <summary>Creates the first token of a new family.</summary>
    /// <param name="tenantId">The owning tenant.</param>
    /// <param name="userId">The user.</param>
    /// <param name="tokenHash">Hash of the issued token.</param>
    /// <param name="issuedAtUtc">Issue time.</param>
    /// <param name="lifetime">How long the token is valid.</param>
    /// <param name="createdByIp">Caller IP.</param>
    /// <returns>The new token record.</returns>
    public static RefreshToken StartFamily(
        Guid tenantId,
        Guid userId,
        string tokenHash,
        DateTime issuedAtUtc,
        TimeSpan lifetime,
        string? createdByIp) =>
        new()
        {
            TenantId = tenantId,
            UserId = userId,
            TokenHash = tokenHash,
            FamilyId = Guid.CreateVersion7(),
            SequenceNumber = 1,
            CreatedAtUtc = issuedAtUtc,
            ExpiresAtUtc = issuedAtUtc.Add(lifetime),
            CreatedByIp = createdByIp,
        };

    /// <summary>Creates the next token in this token's family.</summary>
    /// <param name="tokenHash">Hash of the issued token.</param>
    /// <param name="issuedAtUtc">Issue time.</param>
    /// <param name="lifetime">How long the token is valid.</param>
    /// <param name="createdByIp">Caller IP.</param>
    /// <returns>The successor token record.</returns>
    /// <remarks>
    /// The family expiry is not extended: the successor inherits the original family's
    /// deadline, so an attacker holding a valid chain cannot refresh indefinitely.
    /// </remarks>
    public RefreshToken CreateSuccessor(
        string tokenHash,
        DateTime issuedAtUtc,
        TimeSpan lifetime,
        string? createdByIp)
    {
        DateTime expiry = issuedAtUtc.Add(lifetime);

        return new RefreshToken
        {
            TenantId = TenantId,
            UserId = UserId,
            TokenHash = tokenHash,
            FamilyId = FamilyId,
            SequenceNumber = SequenceNumber + 1,
            CreatedAtUtc = issuedAtUtc,
            ExpiresAtUtc = expiry < ExpiresAtUtc ? expiry : ExpiresAtUtc,
            CreatedByIp = createdByIp,
        };
    }

    /// <summary>Marks this token as revoked.</summary>
    /// <param name="reason">Why.</param>
    /// <param name="utcNow">The current time, in UTC.</param>
    /// <param name="replacedByTokenHash">Hash of the replacement, when rotating.</param>
    public void Revoke(RefreshTokenRevocationReason reason, DateTime utcNow, string? replacedByTokenHash = null)
    {
        RevokedAtUtc = utcNow;
        RevocationReason = reason;
        ReplacedByTokenHash = replacedByTokenHash;
    }
}

/// <summary>
/// Generates refresh tokens and hashes them for storage.
/// </summary>
/// <remarks>
/// Lives in Domain because "the token is never stored in plain text" is a rule about the data,
/// not an infrastructure detail. Only BCL cryptography is used, so the project stays free of
/// infrastructure packages.
/// </remarks>
public static class RefreshTokenGenerator
{
    private const int TokenBytes = 32;

    /// <summary>Generates a cryptographically random token.</summary>
    /// <returns>A URL-safe base64 token.</returns>
    public static string NewToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(TokenBytes))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

    /// <summary>Hashes a token for storage and lookup.</summary>
    /// <param name="token">The token as issued to the client.</param>
    /// <returns>The hex-encoded SHA-256 hash.</returns>
    /// <remarks>
    /// Plain SHA-256 rather than a password hash: the token is 256 bits of entropy from a
    /// cryptographic generator, so there is nothing to brute-force and a deliberately slow
    /// hash would only add latency to every refresh.
    /// </remarks>
    public static string Hash(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}

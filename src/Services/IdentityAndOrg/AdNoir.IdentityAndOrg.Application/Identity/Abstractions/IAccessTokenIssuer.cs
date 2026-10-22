using AdNoir.IdentityAndOrg.Domain.Identity;
using AdNoir.IdentityAndOrg.Domain.Tenants;

namespace AdNoir.IdentityAndOrg.Application.Identity.Abstractions;

/// <summary>An issued access token and when it expires.</summary>
/// <param name="Token">The signed JWT.</param>
/// <param name="ExpiresAtUtc">Expiry, in UTC.</param>
public sealed record IssuedAccessToken(string Token, DateTime ExpiresAtUtc);

/// <summary>
/// Issues signed access tokens.
/// </summary>
/// <remarks>
/// The implementation signs with RS256 using a private key held only by this service; every
/// other service verifies through the JWKS endpoint. Declared here so the application layer
/// stays free of the JWT libraries.
/// </remarks>
public interface IAccessTokenIssuer
{
    /// <summary>Issues an access token for a user.</summary>
    /// <param name="user">The signed-in user.</param>
    /// <param name="tenant">The user's tenant, for the hierarchy and permission-version claims.</param>
    /// <param name="hierarchyPath">The user's organisation path, for display.</param>
    /// <returns>The token and its expiry.</returns>
    IssuedAccessToken Issue(User user, Tenant tenant, string? hierarchyPath);
}

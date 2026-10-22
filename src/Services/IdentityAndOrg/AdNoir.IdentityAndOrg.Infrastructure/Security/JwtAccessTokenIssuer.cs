using System.Globalization;
using System.Security.Claims;
using AdNoir.BuildingBlocks.Configuration;
using AdNoir.BuildingBlocks.Context;
using AdNoir.IdentityAndOrg.Application.Identity.Abstractions;
using AdNoir.IdentityAndOrg.Domain.Identity;
using AdNoir.IdentityAndOrg.Domain.Tenants;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AdNoir.IdentityAndOrg.Infrastructure.Security;

/// <summary>
/// Issues RS256-signed access tokens carrying the user and scope claims.
/// </summary>
/// <remarks>
/// Claims describe the user and scope at issue time. Protected requests validate the live
/// stamp and permission version separately. Permissions come from the cached
/// role matrix; a changed permission version requires refreshed claims.
/// </remarks>
public sealed class JwtAccessTokenIssuer(
    ISigningKeyProvider signingKeyProvider,
    IOptions<JwtOptions> options,
    TimeProvider timeProvider) : IAccessTokenIssuer
{
    private readonly ISigningKeyProvider _signingKeyProvider = signingKeyProvider;
    private readonly JwtOptions _options = options.Value;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly JsonWebTokenHandler _handler = new();

    /// <inheritdoc />
    public IssuedAccessToken Issue(User user, Tenant tenant, string? hierarchyPath)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(tenant);

        DateTime issuedAt = _timeProvider.GetUtcNow().UtcDateTime;
        DateTime expires = issuedAt.AddMinutes(_options.AccessTokenMinutes);

        List<Claim> claims =
        [
            new(AdNoirClaimTypes.Subject, user.Id.ToString()),
            new(AdNoirClaimTypes.TenantId, user.TenantId.ToString()),
            new(AdNoirClaimTypes.OrgUnitId, user.OrgUnitId.ToString()),
            new(AdNoirClaimTypes.PermissionVersion, tenant.PermissionVersion.ToString(CultureInfo.InvariantCulture)),
            new(AdNoirClaimTypes.SecurityStamp, user.SecurityStamp),
            new(AdNoirClaimTypes.HierarchyEnabled, tenant.HierarchyEnabled ? "true" : "false"),
            new(AdNoirClaimTypes.ReadScope, user.EffectiveReadScope.ToString()),
            new(AdNoirClaimTypes.WriteScope, user.EffectiveWriteScope.ToString()),
        ];

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            claims.Add(new Claim(AdNoirClaimTypes.Email, user.Email));
        }

        if (!string.IsNullOrWhiteSpace(hierarchyPath))
        {
            // Readable by anyone holding the token: the JWT is signed, not encrypted. That is
            // a deliberate trade-off rather than an oversight.
            claims.Add(new Claim(AdNoirClaimTypes.HierarchyPath, hierarchyPath));
        }

        foreach (string roleCode in user.RoleCodes)
        {
            claims.Add(new Claim(AdNoirClaimTypes.Roles, roleCode));
        }

        SecurityTokenDescriptor descriptor = new()
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = expires,
            SigningCredentials = new SigningCredentials(
                _signingKeyProvider.SigningKey,
                SecurityAlgorithms.RsaSha256),
        };

        return new IssuedAccessToken(_handler.CreateToken(descriptor), expires);
    }
}

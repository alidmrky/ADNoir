using AdNoir.BuildingBlocks.Context;
using AdNoir.BuildingBlocks.Mongo.Repositories;
using AdNoir.BuildingBlocks.Mongo.Sessions;
using AdNoir.Domain.Abstractions.Repositories;
using AdNoir.IdentityAndOrg.Domain.Identity;
using MongoDB.Driver;

namespace AdNoir.IdentityAndOrg.Infrastructure.Persistence;

/// <summary>MongoDB implementation of <see cref="IRefreshTokenRepository"/>.</summary>
public sealed class MongoRefreshTokenRepository(
    IMongoDatabase database,
    IMongoSessionAccessor sessionAccessor,
    ITenantContext tenantContext,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : MongoRepository<RefreshToken>(database, sessionAccessor, tenantContext, currentUser, timeProvider),
      IRefreshTokenRepository
{
    /// <summary>
    /// Refresh tokens are looked up before anyone is authenticated, so the caller's data scope
    /// cannot apply; the tenant filter still does.
    /// </summary>
    /// <param name="scope">Ignored.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The tenant filter only.</returns>
    protected override Task<FilterDefinition<RefreshToken>> BuildScopeFilterAsync(
        DataScope? scope = null,
        CancellationToken cancellationToken = default) =>
        base.BuildScopeFilterAsync(DataScope.Tenant, cancellationToken);

    /// <inheritdoc />
    [AllowsCrossTenantAccess("An opaque refresh token identifies its tenant before authentication; revoked tokens must remain visible for replay detection.")]
    public async Task<RefreshToken?> FindByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        // No tenant filter here, and no "still usable" filter either. The tenant is unknown
        // until the record is read - the client presents only an opaque token - and reuse
        // detection depends on finding records that are already revoked. Filtering them out
        // would turn a replayed stolen token into a plain "not found".
        IReadOnlyList<RefreshToken> matches = await ((IUnsafeCrossTenantQuery<RefreshToken>)this)
            .ListAcrossTenantsAsync(token => token.TokenHash == tokenHash, cancellationToken);
        return matches.SingleOrDefault();
    }

    /// <inheritdoc />
    public async Task<long> RevokeFamilyAsync(
        Guid familyId,
        RefreshTokenRevocationReason reason,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        FilterDefinition<RefreshToken> filter = Filter.And(
            Filter.Eq(token => token.FamilyId, familyId),
            Filter.Eq(token => token.RevokedAtUtc, null));

        return await RevokeAsync(filter, reason, utcNow, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> RevokeAllForUserAsync(
        Guid userId,
        RefreshTokenRevocationReason reason,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        FilterDefinition<RefreshToken> filter = Filter.And(
            Filter.Eq(token => token.UserId, userId),
            Filter.Eq(token => token.RevokedAtUtc, null));

        return await RevokeAsync(filter, reason, utcNow, cancellationToken);
    }

    private async Task<long> RevokeAsync(
        FilterDefinition<RefreshToken> filter,
        RefreshTokenRevocationReason reason,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        filter &= await BuildScopeFilterAsync(DataScope.Tenant, cancellationToken);
        UpdateDefinition<RefreshToken> update = Builders<RefreshToken>.Update
            .Set(token => token.RevokedAtUtc, utcNow)
            .Set(token => token.RevocationReason, reason);

        IClientSessionHandle session = await SessionAccessor.GetSessionAsync(cancellationToken);

        UpdateResult result = await Collection.UpdateManyAsync(
            session,
            filter,
            update,
            options: null,
            cancellationToken);

        return result.ModifiedCount;
    }
}

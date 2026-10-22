using AdNoir.BuildingBlocks.Context;
using AdNoir.BuildingBlocks.Mongo.Repositories;
using AdNoir.BuildingBlocks.Mongo.Sessions;
using AdNoir.Domain.Abstractions.Repositories;
using AdNoir.IdentityAndOrg.Domain.Tenants;
using MongoDB.Driver;

namespace AdNoir.IdentityAndOrg.Infrastructure.Persistence;

/// <summary>MongoDB implementation of <see cref="ITenantRepository"/>.</summary>
public sealed class MongoTenantRepository(
    IMongoDatabase database,
    IMongoSessionAccessor sessionAccessor,
    ITenantContext tenantContext,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : MongoRepository<Tenant>(database, sessionAccessor, tenantContext, currentUser, timeProvider),
      ITenantRepository
{
    /// <summary>
    /// Tenants are platform-level records, so the caller's data scope does not apply.
    /// </summary>
    /// <param name="scope">Ignored; always treated as tenant-wide.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The soft-delete filter only.</returns>
    /// <remarks>
    /// <see cref="Tenant"/> has no <c>TenantId</c> and belongs to no organisation unit.
    /// Letting the default narrowing run would apply a <c>Self</c> or <c>Unit</c> filter to
    /// the tenant list - filtering tenants by who created them, which is nonsense that would
    /// present itself as "some tenants are missing". Who may read this collection at all is
    /// an authorization question, answered by a platform-level permission.
    /// </remarks>
    protected override Task<FilterDefinition<Tenant>> BuildScopeFilterAsync(
        DataScope? scope = null,
        CancellationToken cancellationToken = default) =>
        base.BuildScopeFilterAsync(DataScope.Tenant, cancellationToken);

    /// <inheritdoc />
    public Task<Tenant?> FindBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        return FindAsync(tenant => tenant.Slug == slug, cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> SlugExistsAsync(
        string slug,
        Guid? excludingId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        return excludingId is Guid id
            ? AnyAsync(tenant => tenant.Slug == slug && tenant.Id != id, cancellationToken)
            : AnyAsync(tenant => tenant.Slug == slug, cancellationToken);
    }
}

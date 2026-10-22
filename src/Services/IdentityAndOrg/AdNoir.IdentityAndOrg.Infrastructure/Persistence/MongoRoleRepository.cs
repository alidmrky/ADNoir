using AdNoir.BuildingBlocks.Context;
using AdNoir.BuildingBlocks.Mongo.Repositories;
using AdNoir.BuildingBlocks.Mongo.Sessions;
using AdNoir.Domain.Abstractions.Repositories;
using AdNoir.IdentityAndOrg.Domain.Identity;
using MongoDB.Driver;

namespace AdNoir.IdentityAndOrg.Infrastructure.Persistence;

/// <summary>MongoDB implementation of <see cref="IRoleRepository"/>.</summary>
public sealed class MongoRoleRepository(
    IMongoDatabase database,
    IMongoSessionAccessor sessionAccessor,
    ITenantContext tenantContext,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : MongoRepository<Role>(database, sessionAccessor, tenantContext, currentUser, timeProvider),
      IRoleRepository
{
    /// <summary>
    /// Roles are filtered by hand rather than by the generic tenant filter.
    /// </summary>
    /// <param name="scope">Ignored.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Soft-delete plus current-tenant or global-system-role visibility.</returns>
    /// <remarks>
    /// <see cref="Role"/> carries a <em>nullable</em> tenant, because system roles are global.
    /// The generic filter matches on equality and would hide exactly those global roles from
    /// every tenant that needs them - SuperAdmin would stop resolving.
    /// </remarks>
    protected override async Task<FilterDefinition<Role>> BuildScopeFilterAsync(
        DataScope? scope = null,
        CancellationToken cancellationToken = default)
    {
        FilterDefinition<Role> visible = Filter.And(
            Filter.Eq(role => role.TenantId, null), Filter.Eq(role => role.IsSystemRole, true));
        if (TenantContext.HasTenant)
        {
            visible |= Filter.And(Filter.Eq(role => role.TenantId, TenantContext.TenantId),
                Filter.Eq(role => role.IsSystemRole, false));
        }
        return await base.BuildScopeFilterAsync(DataScope.Tenant, cancellationToken) & visible;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Role>> ListForTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        FilterDefinition<Role> filter = Filter.And(
            await base.BuildScopeFilterAsync(DataScope.Tenant, cancellationToken),
            Filter.Or(
                Filter.And(Filter.Eq(role => role.TenantId, tenantId), Filter.Eq(role => role.IsSystemRole, false)),
                Filter.And(Filter.Eq(role => role.TenantId, null), Filter.Eq(role => role.IsSystemRole, true))));

        IClientSessionHandle session = await SessionAccessor.GetSessionAsync(cancellationToken);

        return await Collection.Find(session, filter).SortBy(role => role.Code).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Role?> FindByCodeAsync(
        Guid tenantId,
        string code,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        FilterDefinition<Role> filter = Filter.And(
            await base.BuildScopeFilterAsync(DataScope.Tenant, cancellationToken),
            Filter.Eq(role => role.Code, code),
            Filter.Or(
                Filter.And(Filter.Eq(role => role.TenantId, tenantId), Filter.Eq(role => role.IsSystemRole, false)),
                Filter.And(Filter.Eq(role => role.TenantId, null), Filter.Eq(role => role.IsSystemRole, true))));

        IClientSessionHandle session = await SessionAccessor.GetSessionAsync(cancellationToken);

        return await Collection.Find(session, filter).FirstOrDefaultAsync(cancellationToken);
    }
}

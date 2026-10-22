using AdNoir.BuildingBlocks.Context;
using AdNoir.BuildingBlocks.Mongo.Sessions;
using AdNoir.BuildingBlocks.Mongo.Repositories;
using AdNoir.IdentityAndOrg.Domain.OrgUnits;
using MongoDB.Driver;

namespace AdNoir.IdentityAndOrg.Infrastructure.Persistence;

/// <summary>
/// Expands an organisation unit into its subtree, for the <c>Subtree</c> data scope.
/// </summary>
/// <remarks>
/// <para>
/// Queries the collection directly instead of going through <see cref="IOrgUnitRepository"/>.
/// That is not a shortcut: the generic repository takes this resolver as a dependency to
/// build its scope filter, so resolving through the repository would be a dependency cycle -
/// and, worse, a recursive scope evaluation.
/// </para>
/// <para>
/// Only the identifiers are read. The caller needs a list of unit ids to build an <c>$in</c>
/// filter, and materialising whole documents for that would be waste on the hot path of every
/// scoped query.
/// </para>
/// </remarks>
public sealed class OrgUnitSubtreeResolver : IOrgUnitSubtreeResolver
{
    private readonly IMongoCollection<OrgUnit> _collection;
    private readonly ITenantContext _tenantContext;
    private readonly IMongoSessionAccessor _sessions;

    /// <summary>Initializes a new instance.</summary>
    /// <param name="database">The database.</param>
    /// <param name="tenantContext">The current tenant.</param>
    /// <param name="sessions">The current repository transaction session.</param>
    public OrgUnitSubtreeResolver(IMongoDatabase database, ITenantContext tenantContext, IMongoSessionAccessor sessions)
    {
        ArgumentNullException.ThrowIfNull(database);

        _collection = database.GetCollection<OrgUnit>(MongoCollectionNaming.For(typeof(OrgUnit)));
        _tenantContext = tenantContext;
        _sessions = sessions;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> GetSubtreeIdsAsync(
        Guid orgUnitId,
        CancellationToken cancellationToken = default)
    {
        FilterDefinitionBuilder<OrgUnit> filterBuilder = Builders<OrgUnit>.Filter;

        // The tenant filter is applied here too. This class sits outside the generic
        // repository, so it does not inherit that protection and must not skip it.
        FilterDefinition<OrgUnit> filter = filterBuilder.And(
            filterBuilder.Eq(unit => unit.TenantId, _tenantContext.TenantId),
            filterBuilder.Eq(unit => unit.IsDeleted, false),
            filterBuilder.Or(
                filterBuilder.AnyEq(unit => unit.Ancestors, orgUnitId),
                filterBuilder.Eq(unit => unit.Id, orgUnitId)));

        IClientSessionHandle session = await _sessions.GetSessionAsync(cancellationToken);
        List<OrgUnit> matches = await _collection
            .Find(session, filter)
            .Project<OrgUnit>(Builders<OrgUnit>.Projection.Include(unit => unit.Id))
            .ToListAsync(cancellationToken);

        return [.. matches.Select(unit => unit.Id)];
    }
}

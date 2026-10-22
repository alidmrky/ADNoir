using AdNoir.BuildingBlocks.Context;
using AdNoir.BuildingBlocks.Mongo.Repositories;
using AdNoir.BuildingBlocks.Mongo.Sessions;
using AdNoir.Domain.Abstractions.Exceptions;
using AdNoir.Domain.Abstractions.Repositories;
using AdNoir.IdentityAndOrg.Domain.OrgUnits;
using AdNoir.Shared.Contracts.Messages;
using MongoDB.Driver;

namespace AdNoir.IdentityAndOrg.Infrastructure.Persistence;

/// <summary>MongoDB implementation of <see cref="IOrgUnitRepository"/>.</summary>
public sealed class MongoOrgUnitRepository(
    IMongoDatabase database,
    IMongoSessionAccessor sessionAccessor,
    ITenantContext tenantContext,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IOrgUnitSubtreeResolver? subtreeResolver = null)
    : MongoRepository<OrgUnit>(database, sessionAccessor, tenantContext, currentUser, timeProvider, subtreeResolver),
      IOrgUnitRepository
{
    /// <inheritdoc />
    public async Task<OrgUnit?> FindByIdForAuthenticationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        FilterDefinition<OrgUnit> filter = Filter.And(
            await BuildScopeFilterAsync(DataScope.Tenant, cancellationToken),
            Filter.Eq(unit => unit.Id, id));
        IClientSessionHandle session = await SessionAccessor.GetSessionAsync(cancellationToken);
        return await Collection.Find(session, filter).FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OrgUnit>> ListRootsForRegistrationAsync(CancellationToken cancellationToken = default)
    {
        FilterDefinition<OrgUnit> filter = Filter.And(
            await BuildScopeFilterAsync(DataScope.Tenant, cancellationToken),
            Filter.Eq(unit => unit.ParentId, null));
        IClientSessionHandle session = await SessionAccessor.GetSessionAsync(cancellationToken);
        return await Collection.Find(session, filter).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OrgUnit>> ListSubtreeAsync(
        Guid rootId,
        CancellationToken cancellationToken = default)
    {
        // Equality on the Ancestors multikey index, plus the root itself. Deliberately not a
        // path prefix match: /dealer-1 would also match /dealer-10, and an unanchored or
        // case-insensitive regex would not use the index at all.
        FilterDefinition<OrgUnit> filter = Filter.And(
            await BuildScopeFilterAsync(cancellationToken: cancellationToken),
            Filter.Or(
                Filter.AnyEq(unit => unit.Ancestors, rootId),
                Filter.Eq(unit => unit.Id, rootId)));

        IClientSessionHandle session = await SessionAccessor.GetSessionAsync(cancellationToken);

        return await Collection
            .Find(session, filter)
            .SortBy(unit => unit.HierarchyPath)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<OrgUnit>> ListRootsAsync(CancellationToken cancellationToken = default) =>
        ListAsync(unit => unit.ParentId == null, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<OrgUnit>> ListChildrenAsync(
        Guid? parentId,
        CancellationToken cancellationToken = default) =>
        parentId is Guid id
            ? ListAsync(unit => unit.ParentId == id, cancellationToken)
            : ListAsync(unit => unit.ParentId == null, cancellationToken);

    /// <inheritdoc />
    public Task<bool> SlugExistsUnderParentAsync(
        Guid? parentId,
        string slug,
        Guid? excludingId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        if (excludingId is Guid excluded)
        {
            return parentId is Guid parent
                ? AnyAsync(u => u.ParentId == parent && u.Slug == slug && u.Id != excluded, cancellationToken)
                : AnyAsync(u => u.ParentId == null && u.Slug == slug && u.Id != excluded, cancellationToken);
        }

        return parentId is Guid parentValue
            ? AnyAsync(u => u.ParentId == parentValue && u.Slug == slug, cancellationToken)
            : AnyAsync(u => u.ParentId == null && u.Slug == slug, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> ApplySubtreeRewritesAsync(
        IReadOnlyList<OrgUnitRewrite> rewrites,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rewrites);

        if (rewrites.Count == 0)
        {
            return 0;
        }

        IClientSessionHandle session = await SessionAccessor.GetSessionAsync(cancellationToken);
        if (!session.IsInTransaction)
        {
            throw new InvalidOperationException("Subtree rewrites require an active transaction.");
        }
        FilterDefinition<OrgUnit> scopeFilter = await BuildScopeFilterAsync(CurrentUser.WriteScope, cancellationToken);
        List<WriteModel<OrgUnit>> operations = new(rewrites.Count);

        foreach (OrgUnitRewrite rewrite in rewrites)
        {
            // The expected version stays in the filter even for a bulk rewrite. Dropping it
            // "because it is just a path update" would let a concurrent edit inside the
            // subtree be overwritten by a move that never saw it.
            FilterDefinition<OrgUnit> filter = Filter.And(
                scopeFilter,
                Filter.Eq(unit => unit.Id, rewrite.Id),
                Filter.Eq(unit => unit.Version, rewrite.ExpectedVersion));

            UpdateDefinition<OrgUnit> update = Builders<OrgUnit>.Update
                .Set(unit => unit.HierarchyPath, rewrite.HierarchyPath)
                .Set(unit => unit.Ancestors, rewrite.Ancestors)
                .Inc(unit => unit.Version, 1);

            operations.Add(new UpdateOneModel<OrgUnit>(filter, update));
        }

        BulkWriteResult<OrgUnit> result = await Collection.BulkWriteAsync(
            session,
            operations,
            new BulkWriteOptions { IsOrdered = false },
            cancellationToken);

        if (result.MatchedCount != rewrites.Count)
        {
            // Some descendant changed underneath us. Throwing aborts the transaction, so the
            // tree is left exactly as it was - a partially rewritten hierarchy is the one
            // outcome that must not be possible.
            throw new ConcurrencyException(
                MessageCodes.Common.ConcurrencyConflict,
                $"Subtree rewrite matched {result.MatchedCount} of {rewrites.Count} nodes; " +
                "at least one descendant was modified concurrently.");
        }

        return result.ModifiedCount;
    }
}

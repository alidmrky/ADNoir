using System.Linq.Expressions;
using AdNoir.BuildingBlocks.Context;
using AdNoir.BuildingBlocks.Mongo.Sessions;
using AdNoir.Domain.Abstractions.Entities;
using AdNoir.Domain.Abstractions.Exceptions;
using AdNoir.Domain.Abstractions.Repositories;
using AdNoir.Shared.Contracts.Messages;
using MongoDB.Driver;

namespace AdNoir.BuildingBlocks.Mongo.Repositories;

/// <summary>
/// Generic MongoDB repository that applies tenant, soft-delete and data-scope filters
/// automatically.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <remarks>
/// <para>
/// The filters are not optional and not the caller's responsibility. Every read goes through
/// <see cref="BuildScopeFilterAsync"/>, so forgetting a tenant condition is not something a
/// developer can do through this class - which is the whole point.
/// </para>
/// <para>
/// For queries this class cannot express - aggregations, projections, bulk pipelines - it
/// exposes <see cref="Collection"/> and <see cref="BuildScopeFilterAsync"/> to derived
/// repositories. That escape hatch is deliberate: without it, complex queries get written
/// against a raw collection somewhere else in the codebase, with no scope filter at all and
/// nothing to notice it.
/// </para>
/// </remarks>
public class MongoRepository<TEntity> : IRepository<TEntity>, IUnsafeCrossTenantQuery<TEntity>
    where TEntity : class, IEntity
{
    private static readonly bool IsTenantScoped = typeof(IMultiTenant).IsAssignableFrom(typeof(TEntity));
    private static readonly bool IsSoftDeletable = typeof(ISoftDelete).IsAssignableFrom(typeof(TEntity));
    private static readonly bool IsVersioned = typeof(IHasVersion).IsAssignableFrom(typeof(TEntity));
    private static readonly bool IsHierarchyNode = typeof(IHierarchical).IsAssignableFrom(typeof(TEntity));
    private static readonly bool IsUnitScoped = typeof(IOrgUnitScoped).IsAssignableFrom(typeof(TEntity));
    private static readonly bool IsCreationAudited = typeof(ICreationAudited).IsAssignableFrom(typeof(TEntity));

    /// <summary>Initializes a new instance.</summary>
    /// <param name="database">The database.</param>
    /// <param name="sessionAccessor">Supplies the shared session.</param>
    /// <param name="tenantContext">The current tenant.</param>
    /// <param name="currentUser">The current caller.</param>
    /// <param name="timeProvider">Clock, injected so audit stamps are testable.</param>
    /// <param name="subtreeResolver">Expands a unit into its subtree; required only for the subtree scope.</param>
    /// <param name="collectionName">Overrides the conventional collection name.</param>
    public MongoRepository(
        IMongoDatabase database,
        IMongoSessionAccessor sessionAccessor,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        TimeProvider timeProvider,
        IOrgUnitSubtreeResolver? subtreeResolver = null,
        string? collectionName = null)
    {
        ArgumentNullException.ThrowIfNull(database);

        SessionAccessor = sessionAccessor;
        TenantContext = tenantContext;
        CurrentUser = currentUser;
        TimeProvider = timeProvider;
        SubtreeResolver = subtreeResolver;

        Collection = database.GetCollection<TEntity>(
            collectionName ?? MongoCollectionNaming.For(typeof(TEntity)));
    }

    /// <summary>The underlying collection. Escape hatch for queries this class cannot express.</summary>
    protected IMongoCollection<TEntity> Collection { get; }

    /// <summary>Supplies the session every write must go through.</summary>
    protected IMongoSessionAccessor SessionAccessor { get; }

    /// <summary>The current tenant.</summary>
    protected ITenantContext TenantContext { get; }

    /// <summary>The current caller.</summary>
    protected ICurrentUser CurrentUser { get; }

    /// <summary>Clock used for audit stamps.</summary>
    protected TimeProvider TimeProvider { get; }

    /// <summary>Expands a unit into its subtree, when one is registered.</summary>
    protected IOrgUnitSubtreeResolver? SubtreeResolver { get; }

    /// <summary>Filter builder for this entity type.</summary>
    protected static FilterDefinitionBuilder<TEntity> Filter => Builders<TEntity>.Filter;

    // ---------------------------------------------------------------------------------
    // Scope filtering
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Builds the ambient filter: tenant, soft delete and the caller's data scope.
    /// </summary>
    /// <param name="scope">The scope to apply. Defaults to the caller's read scope.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The combined filter.</returns>
    /// <remarks>
    /// Virtual so that a collection which is not tenant-owned can opt out of the data-scope
    /// narrowing. <c>Tenant</c> itself is the example: it has no <c>TenantId</c>, and applying
    /// a <c>Self</c> scope to it would filter the tenant list by whoever happened to create
    /// each record.
    /// </remarks>
    protected virtual async Task<FilterDefinition<TEntity>> BuildScopeFilterAsync(
        DataScope? scope = null,
        CancellationToken cancellationToken = default)
    {
        List<FilterDefinition<TEntity>> filters = [];

        if (IsTenantScoped)
        {
            filters.Add(Filter.Eq(
                new StringFieldDefinition<TEntity, Guid>(nameof(IMultiTenant.TenantId)),
                TenantContext.TenantId));
        }

        if (IsSoftDeletable)
        {
            filters.Add(Filter.Eq(
                new StringFieldDefinition<TEntity, bool>(nameof(ISoftDelete.IsDeleted)),
                false));
        }

        FilterDefinition<TEntity>? scopeFilter =
            await BuildDataScopeFilterAsync(scope ?? CurrentUser.ReadScope, cancellationToken);

        if (scopeFilter is not null)
        {
            filters.Add(scopeFilter);
        }

        return filters.Count == 0 ? Filter.Empty : Filter.And(filters);
    }

    private async Task<FilterDefinition<TEntity>?> BuildDataScopeFilterAsync(
        DataScope scope,
        CancellationToken cancellationToken)
    {
        // Hierarchy off means the tenant filter alone.
        if (!TenantContext.HierarchyEnabled || scope == DataScope.Tenant)
        {
            return null;
        }

        return scope switch
        {
            DataScope.Self => BuildSelfFilter(),
            DataScope.Unit => BuildUnitFilter(),
            DataScope.Subtree => await BuildSubtreeFilterAsync(cancellationToken),
            _ => null,
        };
    }

    private FilterDefinition<TEntity> BuildSelfFilter()
    {
        if (!IsCreationAudited)
        {
            throw new InvalidOperationException(
                $"The Self data scope needs an ownership field, but {typeof(TEntity).Name} implements " +
                $"neither {nameof(ICreationAudited)} nor an alternative. Give the entity an ownership " +
                "field or assign the role a wider scope; silently returning everything is not an option.");
        }

        return Filter.Eq(
            new StringFieldDefinition<TEntity, Guid?>(nameof(ICreationAudited.CreatedBy)),
            CurrentUser.UserId);
    }

    private FilterDefinition<TEntity> BuildUnitFilter()
    {
        Guid orgUnitId = RequireOrgUnit();

        if (IsUnitScoped)
        {
            return Filter.Eq(
                new StringFieldDefinition<TEntity, Guid>(nameof(IOrgUnitScoped.OrgUnitId)),
                orgUnitId);
        }

        if (IsHierarchyNode)
        {
            // The node itself is "its own unit".
            return Filter.Eq(entity => entity.Id, orgUnitId);
        }

        throw new InvalidOperationException(
            $"The Unit data scope cannot be applied to {typeof(TEntity).Name}: it implements neither " +
            $"{nameof(IOrgUnitScoped)} nor {nameof(IHierarchical)}, so there is no field to compare " +
            "the caller's unit against.");
    }

    private async Task<FilterDefinition<TEntity>> BuildSubtreeFilterAsync(CancellationToken cancellationToken)
    {
        Guid orgUnitId = RequireOrgUnit();

        if (IsHierarchyNode)
        {
            // Equality on the multikey Ancestors index, plus the node itself. Faster than a
            // path prefix regex and immune to /dealer-1 matching /dealer-10.
            return Filter.Or(
                Filter.AnyEq(new StringFieldDefinition<TEntity, Guid>(nameof(IHierarchical.Ancestors)), orgUnitId),
                Filter.Eq(entity => entity.Id, orgUnitId));
        }

        if (!IsUnitScoped)
        {
            throw new InvalidOperationException(
                $"The Subtree data scope cannot be applied to {typeof(TEntity).Name}: it implements " +
                $"neither {nameof(IHierarchical)} nor {nameof(IOrgUnitScoped)}.");
        }

        if (SubtreeResolver is null)
        {
            throw new InvalidOperationException(
                $"The Subtree data scope on {typeof(TEntity).Name} requires an " +
                $"{nameof(IOrgUnitSubtreeResolver)} to expand unit {orgUnitId} into its descendants, " +
                "and none is registered. It is provided by the organisation service. " +
                "Failing here is deliberate: guessing the subtree would either leak other units' data " +
                "or hide the caller's own.");
        }

        IReadOnlyList<Guid> subtreeIds = await SubtreeResolver.GetSubtreeIdsAsync(orgUnitId, cancellationToken);

        return Filter.In(
            new StringFieldDefinition<TEntity, Guid>(nameof(IOrgUnitScoped.OrgUnitId)),
            subtreeIds);
    }

    private Guid RequireOrgUnit() =>
        CurrentUser.OrgUnitId ?? throw new InvalidOperationException(
            "The Self, Unit and Subtree data scopes need the caller's organisation unit, but the " +
            "org_unit_id claim is missing from the token.");

    // ---------------------------------------------------------------------------------
    // Reads
    // ---------------------------------------------------------------------------------

    /// <inheritdoc />
    public virtual async Task<TEntity?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        FilterDefinition<TEntity> filter = Filter.And(
            await BuildScopeFilterAsync(cancellationToken: cancellationToken),
            Filter.Eq(entity => entity.Id, id));

        IClientSessionHandle session = await SessionAccessor.GetSessionAsync(cancellationToken);

        return await Collection.Find(session, filter).FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<TEntity?> FindAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        FilterDefinition<TEntity> filter = Filter.And(
            await BuildScopeFilterAsync(cancellationToken: cancellationToken),
            Filter.Where(predicate));

        IClientSessionHandle session = await SessionAccessor.GetSessionAsync(cancellationToken);

        return await Collection.Find(session, filter).FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<IReadOnlyList<TEntity>> ListAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        FilterDefinition<TEntity> filter = await CombineAsync(predicate, cancellationToken);
        IClientSessionHandle session = await SessionAccessor.GetSessionAsync(cancellationToken);

        return await Collection.Find(session, filter).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<IReadOnlyList<TEntity>> ListLimitedAsync(
        int limit,
        Expression<Func<TEntity, bool>>? predicate = null,
        Expression<Func<TEntity, object?>>? orderBy = null,
        bool descending = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        FilterDefinition<TEntity> filter = await CombineAsync(predicate, cancellationToken);
        IClientSessionHandle session = await SessionAccessor.GetSessionAsync(cancellationToken);
        return await Collection.Find(session, filter)
            .Sort(BuildSort(orderBy, descending)).Limit(limit).ToListAsync(cancellationToken);
    }

    private static SortDefinition<TEntity> BuildSort(Expression<Func<TEntity, object?>>? orderBy, bool descending)
    {
        SortDefinition<TEntity> sort = orderBy is null
            ? Builders<TEntity>.Sort.Ascending(entity => entity.Id)
            : descending
                ? Builders<TEntity>.Sort.Descending(orderBy)
                : Builders<TEntity>.Sort.Ascending(orderBy);

        // A non-unique sort key (for example CreatedAtUtc) needs a unique tie-breaker.
        if (orderBy is not null)
        {
            sort = Builders<TEntity>.Sort.Combine(sort, descending
                ? Builders<TEntity>.Sort.Descending(entity => entity.Id)
                : Builders<TEntity>.Sort.Ascending(entity => entity.Id));
        }

        return sort;
    }

    /// <inheritdoc />
    public virtual async Task<PagedResult<TEntity>> ListPagedAsync(
        int pageNumber,
        int pageSize,
        Expression<Func<TEntity, bool>>? predicate = null,
        Expression<Func<TEntity, object?>>? orderBy = null,
        bool descending = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageNumber);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);

        FilterDefinition<TEntity> filter = await CombineAsync(predicate, cancellationToken);
        IClientSessionHandle session = await SessionAccessor.GetSessionAsync(cancellationToken);

        long totalCount = await Collection.CountDocumentsAsync(session, filter, cancellationToken: cancellationToken);

        IFindFluent<TEntity, TEntity> query = Collection.Find(session, filter);

        // Paging without a deterministic sort returns arbitrary pages, and the same document
        // can appear twice or never. Id is the fallback because it is unique and, being a
        // version 7 GUID, also orders by creation time.
        List<TEntity> items = await query
            .Sort(BuildSort(orderBy, descending))
            .Skip((pageNumber - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<TEntity>(items, totalCount);
    }

    /// <inheritdoc />
    public virtual async Task<long> CountAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        FilterDefinition<TEntity> filter = await CombineAsync(predicate, cancellationToken);
        IClientSessionHandle session = await SessionAccessor.GetSessionAsync(cancellationToken);

        return await Collection.CountDocumentsAsync(session, filter, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<bool> AnyAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        FilterDefinition<TEntity> filter = await CombineAsync(predicate, cancellationToken);
        IClientSessionHandle session = await SessionAccessor.GetSessionAsync(cancellationToken);

        return await Collection.Find(session, filter).AnyAsync(cancellationToken);
    }

    // ---------------------------------------------------------------------------------
    // Writes
    // ---------------------------------------------------------------------------------

    /// <inheritdoc />
    public virtual async Task InsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        ApplyInsertMetadata(entity);

        IClientSessionHandle session = await SessionAccessor.GetSessionAsync(cancellationToken);

        await Collection.InsertOneAsync(session, entity, options: null, cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task InsertManyAsync(
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);

        List<TEntity> materialized = [.. entities];

        if (materialized.Count == 0)
        {
            return;
        }

        foreach (TEntity entity in materialized)
        {
            ApplyInsertMetadata(entity);
        }

        IClientSessionHandle session = await SessionAccessor.GetSessionAsync(cancellationToken);

        await Collection.InsertManyAsync(session, materialized, options: null, cancellationToken);
    }

    /// <inheritdoc />
    public virtual Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default) =>
        UpdateWithScopeAsync(entity, CurrentUser.WriteScope, cancellationToken);

    /// <summary>Updates within an explicit data scope while retaining tenant, soft-delete and version filters.</summary>
    /// <param name="entity">The entity to update.</param>
    /// <param name="scope">The operation's data scope.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The completion task.</returns>
    protected async Task UpdateWithScopeAsync(TEntity entity, DataScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);

        FilterDefinition<TEntity> filter = Filter.And(
            await BuildScopeFilterAsync(scope, cancellationToken),
            Filter.Eq(item => item.Id, entity.Id));

        long expectedVersion = 0;

        if (entity is IHasVersion versioned)
        {
            // The expected version goes into the FILTER, not just onto the document. Carrying
            // the field without filtering on it makes it decorative.
            expectedVersion = versioned.Version;

            filter = Filter.And(
                filter,
                Filter.Eq(new StringFieldDefinition<TEntity, long>(nameof(IHasVersion.Version)), expectedVersion));

            versioned.Version = expectedVersion + 1;
        }

        ApplyUpdateMetadata(entity);

        IClientSessionHandle session = await SessionAccessor.GetSessionAsync(cancellationToken);

        ReplaceOneResult result = await Collection.ReplaceOneAsync(
            session,
            filter,
            entity,
            options: new ReplaceOptions { IsUpsert = false },
            cancellationToken);

        if (result.MatchedCount != 0)
        {
            return;
        }

        // Restore the in-memory version so a caller that catches and retries is not left
        // holding a version number the database never accepted.
        if (entity is IHasVersion rejected)
        {
            rejected.Version = expectedVersion;

            throw new ConcurrencyException(
                MessageCodes.Common.ConcurrencyConflict,
                $"{typeof(TEntity).Name} {entity.Id} was modified by another writer " +
                $"(expected version {expectedVersion}).");
        }

        throw new NotFoundException(
            MessageCodes.Common.NotFound,
            $"{typeof(TEntity).Name} {entity.Id} was not found within the caller's write scope.");
    }

    /// <inheritdoc />
    public virtual async Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        await DeleteByIdAsync(entity.Id, cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<bool> DeleteByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        FilterDefinition<TEntity> filter = Filter.And(
            await BuildScopeFilterAsync(CurrentUser.WriteScope, cancellationToken),
            Filter.Eq(entity => entity.Id, id));

        IClientSessionHandle session = await SessionAccessor.GetSessionAsync(cancellationToken);

        if (!IsSoftDeletable)
        {
            DeleteResult deleted = await Collection.DeleteOneAsync(session, filter, options: null, cancellationToken);
            return deleted.DeletedCount > 0;
        }

        UpdateDefinition<TEntity> update = Builders<TEntity>.Update
            .Set(new StringFieldDefinition<TEntity, bool>(nameof(ISoftDelete.IsDeleted)), true)
            .Set(new StringFieldDefinition<TEntity, DateTime?>(nameof(ISoftDelete.DeletedAtUtc)), UtcNow())
            .Set(new StringFieldDefinition<TEntity, Guid?>(nameof(ISoftDelete.DeletedBy)), CurrentUserIdOrNull());

        UpdateResult result = await Collection.UpdateOneAsync(
            session,
            filter,
            update,
            options: null,
            cancellationToken);

        return result.ModifiedCount > 0;
    }

    // ---------------------------------------------------------------------------------
    // Cross-tenant access
    // ---------------------------------------------------------------------------------

    /// <inheritdoc />
    public virtual async Task<TEntity?> FindByIdAcrossTenantsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        IClientSessionHandle session = await SessionAccessor.GetSessionAsync(cancellationToken);

        return await Collection
            .Find(session, Filter.And(NotDeletedFilter(), Filter.Eq(entity => entity.Id, id)))
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<IReadOnlyList<TEntity>> ListAcrossTenantsAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        FilterDefinition<TEntity> filter = predicate is null
            ? NotDeletedFilter()
            : Filter.And(NotDeletedFilter(), Filter.Where(predicate));

        IClientSessionHandle session = await SessionAccessor.GetSessionAsync(cancellationToken);

        return await Collection.Find(session, filter).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<long> CountAcrossTenantsAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        FilterDefinition<TEntity> filter = predicate is null
            ? NotDeletedFilter()
            : Filter.And(NotDeletedFilter(), Filter.Where(predicate));

        IClientSessionHandle session = await SessionAccessor.GetSessionAsync(cancellationToken);

        return await Collection.CountDocumentsAsync(session, filter, cancellationToken: cancellationToken);
    }

    // ---------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------

    private async Task<FilterDefinition<TEntity>> CombineAsync(
        Expression<Func<TEntity, bool>>? predicate,
        CancellationToken cancellationToken)
    {
        FilterDefinition<TEntity> scopeFilter = await BuildScopeFilterAsync(cancellationToken: cancellationToken);

        return predicate is null ? scopeFilter : Filter.And(scopeFilter, Filter.Where(predicate));
    }

    /// <summary>
    /// Soft-delete filter without the tenant condition, for cross-tenant reads. Deleted
    /// records stay hidden even here: crossing tenants is authorised, resurrecting deleted
    /// data is a separate decision.
    /// </summary>
    private static FilterDefinition<TEntity> NotDeletedFilter() =>
        IsSoftDeletable
            ? Filter.Eq(new StringFieldDefinition<TEntity, bool>(nameof(ISoftDelete.IsDeleted)), false)
            : Filter.Empty;

    private void ApplyInsertMetadata(TEntity entity)
    {
        if (entity.Id == Guid.Empty)
        {
            entity.Id = Guid.CreateVersion7();
        }

        if (IsTenantScoped && entity is IMultiTenant tenantOwned && tenantOwned.TenantId == Guid.Empty)
        {
            tenantOwned.TenantId = TenantContext.TenantId;
        }

        if (entity is ICreationAudited created)
        {
            created.CreatedAtUtc = UtcNow();
            created.CreatedBy ??= CurrentUserIdOrNull();
        }

        if (IsVersioned && entity is IHasVersion versioned && versioned.Version == 0)
        {
            versioned.Version = 1;
        }
    }

    private void ApplyUpdateMetadata(TEntity entity)
    {
        if (entity is IModificationAudited modified)
        {
            modified.UpdatedAtUtc = UtcNow();
            modified.UpdatedBy = CurrentUserIdOrNull();
        }
    }

    private DateTime UtcNow() => TimeProvider.GetUtcNow().UtcDateTime;

    /// <summary>
    /// The caller's identifier, or null for work with no authenticated user - the migrator,
    /// a background job, a consumer handling a message.
    /// </summary>
    private Guid? CurrentUserIdOrNull() => CurrentUser.IsAuthenticated ? CurrentUser.UserId : null;
}

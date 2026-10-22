using System.Linq.Expressions;
using AdNoir.Domain.Abstractions.Entities;

namespace AdNoir.Domain.Abstractions.Repositories;

/// <summary>A page of entities together with the total number of matches.</summary>
/// <typeparam name="T">The entity type.</typeparam>
/// <param name="Items">The entities on the requested page.</param>
/// <param name="TotalCount">Total matches across all pages.</param>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, long TotalCount);

/// <summary>
/// Generic persistence for an entity type. Implemented in <c>AdNoir.BuildingBlocks.Mongo</c>;
/// declared here so that domain and application code never see the database technology.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <remarks>
/// <para>
/// Every method here applies the ambient filters automatically: the tenant filter for
/// <see cref="IMultiTenant"/> entities, the not-deleted filter for <see cref="ISoftDelete"/>
/// entities, and the caller's read or write scope. Forgetting a tenant filter is meant to be
/// <em>impossible</em> through this interface; crossing tenants requires the
/// explicitly-named <see cref="IUnsafeCrossTenantQuery{TEntity}"/>.
/// </para>
/// <para>
/// For queries this interface cannot express, the concrete repository exposes a protected
/// escape hatch (the collection plus a scope-filter builder) rather than forcing callers to
/// bypass the repository entirely — which is what actually happens when no escape hatch
/// exists.
/// </para>
/// </remarks>
public interface IRepository<TEntity>
    where TEntity : class, IEntity
{
    /// <summary>Finds an entity by its identifier within the current scope.</summary>
    /// <param name="id">The entity identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The entity, or null when it does not exist or is out of scope.</returns>
    Task<TEntity?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Finds the first entity matching a predicate within the current scope.</summary>
    /// <param name="predicate">The condition to match.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The entity, or null when nothing matches.</returns>
    Task<TEntity?> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>Lists entities matching an optional predicate within the current scope.</summary>
    /// <param name="predicate">The condition to match, or null for everything in scope.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching entities.</returns>
    Task<IReadOnlyList<TEntity>> ListAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns at most the requested number of scoped entities, without counting all matches.</summary>
    /// <param name="limit">Positive maximum number of entities.</param>
    /// <param name="predicate">Optional matching condition.</param>
    /// <param name="orderBy">Optional sort key; Id provides deterministic tie-breaking.</param>
    /// <param name="descending">Whether to reverse the sort and tie-breaker.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The bounded list, without a total count.</returns>
    Task<IReadOnlyList<TEntity>> ListLimitedAsync(
        int limit,
        Expression<Func<TEntity, bool>>? predicate = null,
        Expression<Func<TEntity, object?>>? orderBy = null,
        bool descending = false,
        CancellationToken cancellationToken = default);

    /// <summary>Lists one page of entities within the current scope.</summary>
    /// <param name="pageNumber">The 1-based page number.</param>
    /// <param name="pageSize">Maximum number of items per page.</param>
    /// <param name="predicate">The condition to match, or null for everything in scope.</param>
    /// <param name="orderBy">Sort key. Paging without a stable sort returns arbitrary pages.</param>
    /// <param name="descending">Whether to sort in descending order.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The page and the total number of matches.</returns>
    Task<PagedResult<TEntity>> ListPagedAsync(
        int pageNumber,
        int pageSize,
        Expression<Func<TEntity, bool>>? predicate = null,
        Expression<Func<TEntity, object?>>? orderBy = null,
        bool descending = false,
        CancellationToken cancellationToken = default);

    /// <summary>Counts entities matching an optional predicate within the current scope.</summary>
    /// <param name="predicate">The condition to match, or null for everything in scope.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of matches.</returns>
    Task<long> CountAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default);

    /// <summary>Determines whether any entity matches within the current scope.</summary>
    /// <param name="predicate">The condition to match, or null for everything in scope.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when at least one entity matches.</returns>
    Task<bool> AnyAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default);

    /// <summary>Inserts an entity, filling audit and tenant fields automatically.</summary>
    /// <param name="entity">The entity to insert.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the insert has been issued.</returns>
    Task InsertAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>Inserts several entities in one round trip.</summary>
    /// <param name="entities">The entities to insert.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the insert has been issued.</returns>
    Task InsertManyAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an entity. For <see cref="IHasVersion"/> entities the expected version takes
    /// part in the filter and is incremented, so a concurrent write raises
    /// <see cref="Exceptions.ConcurrencyException"/> instead of being silently overwritten.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the update has been issued.</returns>
    Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an entity: soft delete when it implements <see cref="ISoftDelete"/>,
    /// a real delete otherwise.
    /// </summary>
    /// <param name="entity">The entity to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the delete has been issued.</returns>
    Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>Deletes an entity by its identifier, within the current scope.</summary>
    /// <param name="id">The entity identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when an entity was deleted.</returns>
    Task<bool> DeleteByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

using System.Linq.Expressions;
using AdNoir.Domain.Abstractions.Entities;

namespace AdNoir.Domain.Abstractions.Repositories;

/// <summary>
/// Reads across tenant boundaries. The name is deliberately alarming.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <remarks>
/// <para>
/// Some operations genuinely need this — a platform-wide report, a background job that
/// sweeps every tenant, the migrator. Making it impossible would only push people into
/// bypassing the repository altogether, which is worse because it leaves no trace.
/// </para>
/// <para>
/// So the escape is allowed but never quiet: every method that calls into this interface
/// must carry <see cref="AllowsCrossTenantAccessAttribute"/> with a written justification,
/// and an architecture test scans method bodies and fails the build otherwise. The result is
/// that each leak point stays visible in code, next to the reason it exists.
/// </para>
/// </remarks>
public interface IUnsafeCrossTenantQuery<TEntity>
    where TEntity : class, IEntity
{
    /// <summary>Finds an entity by identifier in any tenant.</summary>
    /// <param name="id">The entity identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The entity, or null when it does not exist.</returns>
    Task<TEntity?> FindByIdAcrossTenantsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Lists entities in any tenant.</summary>
    /// <param name="predicate">The condition to match, or null for everything.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching entities across all tenants.</returns>
    Task<IReadOnlyList<TEntity>> ListAcrossTenantsAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default);

    /// <summary>Counts entities in any tenant.</summary>
    /// <param name="predicate">The condition to match, or null for everything.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of matches across all tenants.</returns>
    Task<long> CountAcrossTenantsAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default);
}

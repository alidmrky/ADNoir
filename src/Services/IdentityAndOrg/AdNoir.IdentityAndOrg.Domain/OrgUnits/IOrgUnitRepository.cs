using AdNoir.Domain.Abstractions.Repositories;

namespace AdNoir.IdentityAndOrg.Domain.OrgUnits;

/// <summary>
/// Organisation unit persistence. Declared in Domain, implemented in Infrastructure.
/// </summary>
public interface IOrgUnitRepository : IRepository<OrgUnit>
{
    /// <summary>Resolves the account's unit for token claims within the current tenant.</summary>
    Task<OrgUnit?> FindByIdForAuthenticationAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Resolves registration roots within the current tenant before a caller exists.</summary>
    Task<IReadOnlyList<OrgUnit>> ListRootsForRegistrationAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Every unit beneath a node, plus the node itself.
    /// </summary>
    /// <param name="rootId">The subtree root.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The subtree.</returns>
    /// <remarks>
    /// Implemented as an equality match on the <c>Ancestors</c> multikey index, not as a path
    /// prefix regex.
    /// </remarks>
    Task<IReadOnlyList<OrgUnit>> ListSubtreeAsync(Guid rootId, CancellationToken cancellationToken = default);

    /// <summary>The tenant's root units.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Units with no parent.</returns>
    Task<IReadOnlyList<OrgUnit>> ListRootsAsync(CancellationToken cancellationToken = default);

    /// <summary>The direct children of a node.</summary>
    /// <param name="parentId">The parent, or null for roots.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The children.</returns>
    Task<IReadOnlyList<OrgUnit>> ListChildrenAsync(
        Guid? parentId,
        CancellationToken cancellationToken = default);

    /// <summary>Whether a slug is already used by a sibling.</summary>
    /// <param name="parentId">The parent, or null for roots.</param>
    /// <param name="slug">The slug to check.</param>
    /// <param name="excludingId">A unit to ignore, used when updating that unit.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when a sibling already uses the slug.</returns>
    Task<bool> SlugExistsUnderParentAsync(
        Guid? parentId,
        string slug,
        Guid? excludingId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a set of subtree rewrites in one bulk operation.
    /// </summary>
    /// <param name="rewrites">The rewrites, as computed by <see cref="OrgUnitTree"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>How many documents were updated.</returns>
    /// <remarks>
    /// One bulk write inside the caller's transaction. A partially updated tree must never be
    /// left behind, so this either applies to every node or to none - and it
    /// keeps each node's version in the filter, so a concurrent edit inside the subtree is
    /// detected instead of being overwritten.
    /// </remarks>
    Task<long> ApplySubtreeRewritesAsync(
        IReadOnlyList<OrgUnitRewrite> rewrites,
        CancellationToken cancellationToken = default);
}

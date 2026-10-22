namespace AdNoir.BuildingBlocks.Mongo.Repositories;

/// <summary>
/// Expands an organisation unit into the identifiers of its whole subtree.
/// </summary>
/// <remarks>
/// <para>
/// Needed for the <c>Subtree</c> data scope on entities that merely reference a unit rather
/// than being hierarchy nodes themselves. A node entity can be filtered directly on its
/// <c>Ancestors</c> array; an order that carries an <c>OrgUnitId</c> cannot, because the
/// order does not know where its unit sits in the tree.
/// </para>
/// <para>
/// Implemented by the organisation service, which owns the tree and caches it. When no
/// resolver is registered the repository throws rather than guessing - a scope that silently
/// widens is a data leak, and one that silently narrows is a bug report that reads "records
/// are missing sometimes".
/// </para>
/// </remarks>
public interface IOrgUnitSubtreeResolver
{
    /// <summary>Returns the unit and every unit beneath it.</summary>
    /// <param name="orgUnitId">The root of the subtree.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The identifiers of the unit and its descendants.</returns>
    Task<IReadOnlyList<Guid>> GetSubtreeIdsAsync(Guid orgUnitId, CancellationToken cancellationToken = default);
}

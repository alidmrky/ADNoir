using AdNoir.BuildingBlocks.Services;
using AdNoir.IdentityAndOrg.Application.OrgUnits.Dtos;
using AdNoir.Shared.Contracts.Responses;

namespace AdNoir.IdentityAndOrg.Application.OrgUnits;

/// <summary>Organisation hierarchy operations.</summary>
public interface IOrgUnitService : IApplicationService
{
    /// <summary>Gets a unit by identifier.</summary>
    /// <param name="id">The unit identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The unit, or a not-found response.</returns>
    Task<BaseResponse<OrgUnitDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Lists units within the caller's scope.</summary>
    /// <param name="pageNumber">1-based page number.</param>
    /// <param name="pageSize">Items per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A page of units.</returns>
    Task<PagedResponse<OrgUnitDto>> ListAsync(
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    /// <summary>Lists the direct children of a unit.</summary>
    /// <param name="parentId">The parent, or null for roots.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The children.</returns>
    Task<BaseResponse<IReadOnlyList<OrgUnitDto>>> ListChildrenAsync(
        Guid? parentId,
        CancellationToken cancellationToken = default);

    /// <summary>Lists a unit and everything beneath it.</summary>
    /// <param name="rootId">The subtree root.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The subtree, ordered by path.</returns>
    Task<BaseResponse<IReadOnlyList<OrgUnitDto>>> ListSubtreeAsync(
        Guid rootId,
        CancellationToken cancellationToken = default);

    /// <summary>Creates a unit.</summary>
    /// <param name="request">The creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created unit, or a failure response.</returns>
    Task<BaseResponse<OrgUnitDto>> CreateAsync(
        CreateOrgUnitRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames a unit, and re-slugs it when a new slug is supplied.
    /// </summary>
    /// <param name="id">The unit identifier.</param>
    /// <param name="request">The rename request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated unit, or a failure response.</returns>
    /// <remarks>
    /// A slug change rewrites the whole subtree's paths, exactly as a move does. This is the
    /// trigger that most often gets missed.
    /// </remarks>
    Task<BaseResponse<OrgUnitDto>> RenameAsync(
        Guid id,
        RenameOrgUnitRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Moves a unit under a different parent.</summary>
    /// <param name="id">The unit identifier.</param>
    /// <param name="request">The move request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The moved unit, or a failure response.</returns>
    Task<BaseResponse<OrgUnitDto>> MoveAsync(
        Guid id,
        MoveOrgUnitRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes a unit that has no children.</summary>
    /// <param name="id">The unit identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An empty success, or a failure response.</returns>
    Task<BaseResponse> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

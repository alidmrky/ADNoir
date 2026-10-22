using AdNoir.BuildingBlocks.Services;
using AdNoir.IdentityAndOrg.Application.Tenants.Dtos;
using AdNoir.Shared.Contracts.Responses;

namespace AdNoir.IdentityAndOrg.Application.Tenants;

/// <summary>Tenant operations.</summary>
/// <remarks>
/// Every method returns the response envelope, is asynchronous and takes a
/// <see cref="CancellationToken"/> last. An architecture test enforces all three.
/// </remarks>
public interface ITenantService : IApplicationService
{
    /// <summary>Gets a tenant by identifier.</summary>
    /// <param name="id">The tenant identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The tenant, or a not-found response.</returns>
    Task<BaseResponse<TenantDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Gets a tenant by slug.</summary>
    /// <param name="slug">The tenant slug.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The tenant, or a not-found response.</returns>
    Task<BaseResponse<TenantDto>> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>Lists tenants.</summary>
    /// <param name="pageNumber">1-based page number.</param>
    /// <param name="pageSize">Items per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A page of tenants.</returns>
    Task<PagedResponse<TenantDto>> ListAsync(
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    /// <summary>Creates a tenant.</summary>
    /// <param name="request">The creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created tenant, or a conflict when the slug is taken.</returns>
    Task<BaseResponse<TenantDto>> CreateAsync(
        CreateTenantRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Updates a tenant.</summary>
    /// <param name="id">The tenant identifier.</param>
    /// <param name="request">The update request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated tenant, or a not-found or concurrency response.</returns>
    Task<BaseResponse<TenantDto>> UpdateAsync(
        Guid id,
        UpdateTenantRequest request,
        CancellationToken cancellationToken = default);
}

using AdNoir.BuildingBlocks.Services;
using AdNoir.IdentityAndOrg.Application.Identity.Dtos;
using AdNoir.Shared.Contracts.Responses;

namespace AdNoir.IdentityAndOrg.Application.Identity;

/// <summary>User administration.</summary>
public interface IUserService : IApplicationService
{
    /// <summary>Gets a user.</summary>
    /// <param name="id">The user identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user, or a not-found response.</returns>
    Task<BaseResponse<UserDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Lists users within the caller's scope.</summary>
    /// <param name="pageNumber">1-based page number.</param>
    /// <param name="pageSize">Items per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A page of users.</returns>
    Task<PagedResponse<UserDto>> ListAsync(
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    /// <summary>Creates a user.</summary>
    /// <param name="request">The creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created user, or a failure response.</returns>
    Task<BaseResponse<UserDto>> CreateAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Replaces a user's role assignments.</summary>
    /// <param name="id">The user identifier.</param>
    /// <param name="request">The new assignments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated user, or a failure response.</returns>
    Task<BaseResponse<UserDto>> AssignRolesAsync(
        Guid id,
        AssignRolesRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Activates or deactivates a user.</summary>
    /// <param name="id">The user identifier.</param>
    /// <param name="isActive">The desired state.</param>
    /// <param name="expectedVersion">The version the caller last read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated user, or a failure response.</returns>
    Task<BaseResponse<UserDto>> SetActiveAsync(
        Guid id,
        bool isActive,
        long expectedVersion,
        CancellationToken cancellationToken = default);

    /// <summary>Replaces a user's forgotten password with a temporary one.</summary>
    /// <param name="id">The user identifier.</param>
    /// <param name="request">The temporary password and expected version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated user, or a failure response.</returns>
    /// <remarks>
    /// Ends every session of the user, clears any lockout and forces a password change at next
    /// sign-in. Callers can only reset accounts whose roles they could grant themselves.
    /// </remarks>
    Task<BaseResponse<UserDto>> ResetPasswordAsync(
        Guid id,
        ResetUserPasswordRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Role administration.</summary>
public interface IRoleService : IApplicationService
{
    /// <summary>Lists the roles available to the current tenant, including system roles.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The roles.</returns>
    Task<BaseResponse<IReadOnlyList<RoleDto>>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets a role.</summary>
    /// <param name="id">The role identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The role, or a not-found response.</returns>
    Task<BaseResponse<RoleDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Creates a tenant role.</summary>
    /// <param name="request">The creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created role, or a failure response.</returns>
    Task<BaseResponse<RoleDto>> CreateAsync(
        CreateRoleRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Updates a role's name and permissions.</summary>
    /// <param name="id">The role identifier.</param>
    /// <param name="request">The update request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated role, or a failure response.</returns>
    /// <remarks>
    /// Changing permissions bumps the tenant's permission-matrix version, which is what
    /// invalidates every service's cached copy.
    /// </remarks>
    Task<BaseResponse<RoleDto>> UpdateAsync(
        Guid id,
        UpdateRoleRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes a tenant role.</summary>
    /// <param name="id">The role identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An empty success, or a failure response.</returns>
    Task<BaseResponse> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

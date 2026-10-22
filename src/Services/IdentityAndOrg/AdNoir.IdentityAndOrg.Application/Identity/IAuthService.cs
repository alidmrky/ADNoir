using AdNoir.BuildingBlocks.Services;
using AdNoir.IdentityAndOrg.Application.Identity.Dtos;
using AdNoir.Shared.Contracts.Responses;

namespace AdNoir.IdentityAndOrg.Application.Identity;

/// <summary>Sign-in, registration and token lifecycle.</summary>
public interface IAuthService : IApplicationService
{
    /// <summary>Registers a user in a tenant.</summary>
    /// <param name="request">The registration request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created user, or a failure response.</returns>
    Task<BaseResponse<UserDto>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Signs a user in.</summary>
    /// <param name="request">The sign-in request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Access and refresh tokens, or a failure response.</returns>
    Task<BaseResponse<AuthTokensDto>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Exchanges a refresh token for a new pair.</summary>
    /// <param name="request">The refresh request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>New tokens, or a failure response.</returns>
    Task<BaseResponse<AuthTokensDto>> RefreshAsync(
        RefreshRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Revokes a refresh token, and optionally every session the user holds.</summary>
    /// <param name="request">The sign-out request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An empty success.</returns>
    Task<BaseResponse> LogoutAsync(
        LogoutRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Changes the signed-in user's password.</summary>
    /// <param name="request">The change request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An empty success, or a failure response.</returns>
    Task<BaseResponse> ChangePasswordAsync(
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default);
}

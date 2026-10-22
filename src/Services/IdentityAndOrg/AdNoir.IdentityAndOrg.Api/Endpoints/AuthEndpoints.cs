using AdNoir.BuildingBlocks.Http;
using AdNoir.BuildingBlocks.Authorization;
using AdNoir.IdentityAndOrg.Application.Identity;
using AdNoir.IdentityAndOrg.Application.Identity.Dtos;
using AdNoir.IdentityAndOrg.Infrastructure.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AdNoir.IdentityAndOrg.Api.Endpoints;

/// <summary>Sign-in, registration and token endpoints.</summary>
public static class AuthEndpoints
{
    /// <summary>Maps the authentication endpoints.</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        RouteGroupBuilder group = app
            .MapGroup("/api/auth")
            .WithTags("Authentication");

        group.MapPost("/register", async (
                RegisterRequest request,
                IAuthService service,
                CancellationToken cancellationToken) =>
            {
                return (await service.RegisterAsync(request, cancellationToken)).ToHttpResult();
            })
            .AllowAnonymous()
            .WithName("Register")
            .WithSummary("Registers a user in a tenant.")
            .WithValidation<RegisterRequest>()
            .ProducesEnvelope<UserDto>(StatusCodes.Status201Created);

        group.MapPost("/login", async (
                LoginRequest request,
                IAuthService service,
                CancellationToken cancellationToken) =>
            {
                return (await service.LoginAsync(request, cancellationToken)).ToHttpResult();
            })
            .AllowAnonymous()
            .WithName("Login")
            .WithSummary("Signs a user in and returns an access and refresh token pair.")
            .WithDescription(
                "Unknown accounts, wrong passwords and unknown tenants all return the same " +
                "message and take the same time, so the endpoint cannot be used to discover " +
                "which addresses are registered.")
            .WithValidation<LoginRequest>()
            .ProducesEnvelope<AuthTokensDto>();

        group.MapPost("/refresh", async (
                RefreshRequest request,
                IAuthService service,
                CancellationToken cancellationToken) =>
            {
                return (await service.RefreshAsync(request, cancellationToken)).ToHttpResult();
            })
            .AllowAnonymous()
            .WithName("RefreshToken")
            .WithSummary("Exchanges a refresh token for a new pair.")
            .WithDescription(
                "Rotating: the presented token is revoked and a successor is issued in the same " +
                "family. Presenting an already-revoked token revokes the whole family, because " +
                "that is what a replayed stolen token looks like.")
            .WithValidation<RefreshRequest>()
            .ProducesEnvelope<AuthTokensDto>();

        group.MapPost("/logout", async (
                LogoutRequest request,
                IAuthService service,
                CancellationToken cancellationToken) =>
            {
                return (await service.LogoutAsync(request, cancellationToken)).ToHttpResult();
            })
            .AllowAnonymous()
            .WithName("Logout")
            .WithSummary("Revokes a refresh token, or every session the user holds.")
            .WithValidation<LogoutRequest>()
            .ProducesEnvelope();

        // The only endpoint in this group that needs an authenticated caller: the current
        // password alone is not enough to authorise a change.
        group.MapPost("/change-password", async (
                ChangePasswordRequest request,
                IAuthService service,
                CancellationToken cancellationToken) =>
            {
                return (await service.ChangePasswordAsync(request, cancellationToken)).ToHttpResult();
            })
            .WithMetadata(new AllowPasswordChangeRequiredAttribute())
            .WithName("ChangePassword")
            .WithSummary("Changes the signed-in user's password and ends every session.")
            .WithValidation<ChangePasswordRequest>()
            .ProducesEnvelope()
            .RequireAuthorization();

        return app;
    }

    /// <summary>
    /// Maps the JWKS document other services verify tokens against.
    /// </summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder.</returns>
    /// <remarks>
    /// Anonymous and cacheable by design: it carries only public keys, and every service in
    /// the system needs to read it before it can validate anything.
    /// </remarks>
    public static IEndpointRouteBuilder MapJwksEndpoint(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/.well-known/jwks.json", (ISigningKeyProvider keyProvider) =>
            {
                return Results.Json(keyProvider.GetPublicKeySet(), contentType: "application/jwk-set+json");
            })
            .WithName("Jwks")
            .WithTags("Authentication")
            .WithSummary("Public keys for verifying AdNoir access tokens.")
            .AllowAnonymous()
            .CacheOutput(policy => policy.Expire(TimeSpan.FromMinutes(10)));

        return app;
    }
}

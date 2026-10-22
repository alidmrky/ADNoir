using AdNoir.BuildingBlocks.Authorization;
using AdNoir.BuildingBlocks.Http;
using AdNoir.IdentityAndOrg.Application.Identity;
using AdNoir.IdentityAndOrg.Application.Identity.Dtos;
using AdNoir.Shared.Contracts.Permissions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AdNoir.IdentityAndOrg.Api.Endpoints;

/// <summary>User administration endpoints.</summary>
public static class UserEndpoints
{
    /// <summary>Maps the user endpoints.</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        RouteGroupBuilder group = app.MapGroup("/api/v1/users").WithTags("Users");

        group.MapGet("/", async (
                IUserService service,
                CancellationToken cancellationToken,
                int pageNumber = 1,
                int pageSize = 20) =>
            {
                return (await service.ListAsync(pageNumber, pageSize, cancellationToken)).ToHttpResult();
            })
            .WithName("ListUsers")
            .WithSummary("Lists users within the caller's scope.")
            .ProducesPagedEnvelope<UserDto>()
            .RequirePermission(PermissionCatalog.Users.Read);

        group.MapGet("/{id:guid}", async (
                Guid id,
                IUserService service,
                CancellationToken cancellationToken) =>
            {
                return (await service.GetByIdAsync(id, cancellationToken)).ToHttpResult();
            })
            .WithName("GetUser")
            .WithSummary("Gets a user.")
            .ProducesEnvelope<UserDto>()
            .RequirePermission(PermissionCatalog.Users.Read);

        group.MapPost("/", async (
                CreateUserRequest request,
                IUserService service,
                CancellationToken cancellationToken) =>
            {
                return (await service.CreateAsync(request, cancellationToken)).ToHttpResult();
            })
            .WithName("CreateUser")
            .WithSummary("Creates a user in the current tenant.")
            .WithValidation<CreateUserRequest>()
            .ProducesEnvelope<UserDto>(StatusCodes.Status201Created)
            .RequirePermission(PermissionCatalog.Users.Write);

        group.MapPut("/{id:guid}/roles", async (
                Guid id,
                AssignRolesRequest request,
                IUserService service,
                CancellationToken cancellationToken) =>
            {
                return (await service.AssignRolesAsync(id, request, cancellationToken)).ToHttpResult();
            })
            .WithName("AssignUserRoles")
            .WithSummary("Replaces a user's role assignments.")
            .WithDescription(
                "Rotates the user's security stamp, so tokens issued before the change are " +
                "reissued with new claims at the next refresh.")
            .WithValidation<AssignRolesRequest>()
            .ProducesEnvelope<UserDto>()
            .RequirePermission(PermissionCatalog.Users.Manage);

        group.MapPost("/{id:guid}/activate", async (
                Guid id,
                long expectedVersion,
                IUserService service,
                CancellationToken cancellationToken) =>
            {
                return (await service.SetActiveAsync(id, true, expectedVersion, cancellationToken)).ToHttpResult();
            })
            .WithName("ActivateUser")
            .WithSummary("Reactivates a user.")
            .ProducesEnvelope<UserDto>()
            .RequirePermission(PermissionCatalog.Users.Manage);

        group.MapPost("/{id:guid}/deactivate", async (
                Guid id,
                long expectedVersion,
                IUserService service,
                CancellationToken cancellationToken) =>
            {
                return (await service.SetActiveAsync(id, false, expectedVersion, cancellationToken)).ToHttpResult();
            })
            .WithName("DeactivateUser")
            .WithSummary("Deactivates a user and revokes every session they hold.")
            .ProducesEnvelope<UserDto>()
            .RequirePermission(PermissionCatalog.Users.Manage);

        return app;
    }

    /// <summary>Maps the role endpoints.</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapRoleEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        RouteGroupBuilder group = app.MapGroup("/api/v1/roles").WithTags("Roles");

        group.MapGet("/", async (IRoleService service, CancellationToken cancellationToken) =>
            {
                return (await service.ListAsync(cancellationToken)).ToHttpResult();
            })
            .WithName("ListRoles")
            .WithSummary("Lists the roles available to the current tenant, including system roles.")
            .ProducesEnvelope<IReadOnlyList<RoleDto>>()
            .RequirePermission(PermissionCatalog.Roles.Read);

        group.MapGet("/{id:guid}", async (
                Guid id,
                IRoleService service,
                CancellationToken cancellationToken) =>
            {
                return (await service.GetByIdAsync(id, cancellationToken)).ToHttpResult();
            })
            .WithName("GetRole")
            .WithSummary("Gets a role.")
            .ProducesEnvelope<RoleDto>()
            .RequirePermission(PermissionCatalog.Roles.Read);

        group.MapPost("/", async (
                CreateRoleRequest request,
                IRoleService service,
                CancellationToken cancellationToken) =>
            {
                return (await service.CreateAsync(request, cancellationToken)).ToHttpResult();
            })
            .WithName("CreateRole")
            .WithSummary("Creates a tenant role.")
            .WithValidation<CreateRoleRequest>()
            .ProducesEnvelope<RoleDto>(StatusCodes.Status201Created)
            .RequirePermission(PermissionCatalog.Roles.Manage);

        group.MapPut("/{id:guid}", async (
                Guid id,
                UpdateRoleRequest request,
                IRoleService service,
                CancellationToken cancellationToken) =>
            {
                return (await service.UpdateAsync(id, request, cancellationToken)).ToHttpResult();
            })
            .WithName("UpdateRole")
            .WithSummary("Updates a role's name and permissions.")
            .WithDescription(
                "Bumps the tenant's permission-matrix version, which invalidates every service's " +
                "cached copy without an eviction message.")
            .WithValidation<UpdateRoleRequest>()
            .ProducesEnvelope<RoleDto>()
            .RequirePermission(PermissionCatalog.Roles.Manage);

        group.MapDelete("/{id:guid}", async (
                Guid id,
                IRoleService service,
                CancellationToken cancellationToken) =>
            {
                return (await service.DeleteAsync(id, cancellationToken)).ToHttpResult();
            })
            .WithName("DeleteRole")
            .WithSummary("Deletes a tenant role that nobody holds.")
            .ProducesEnvelope()
            .RequirePermission(PermissionCatalog.Roles.Manage);

        return app;
    }
}

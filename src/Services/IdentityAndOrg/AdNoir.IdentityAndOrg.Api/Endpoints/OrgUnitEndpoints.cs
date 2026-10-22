using AdNoir.BuildingBlocks.Authorization;
using AdNoir.BuildingBlocks.Http;
using AdNoir.IdentityAndOrg.Application.OrgUnits;
using AdNoir.IdentityAndOrg.Application.OrgUnits.Dtos;
using AdNoir.Shared.Contracts.Permissions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AdNoir.IdentityAndOrg.Api.Endpoints;

/// <summary>Organisation unit endpoints.</summary>
public static class OrgUnitEndpoints
{
    /// <summary>Maps the organisation unit endpoints.</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapOrgUnitEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        RouteGroupBuilder group = app
            .MapGroup("/api/v1/org-units")
            .WithTags("Organisation units");

        group.MapGet("/", async (
                IOrgUnitService service,
                CancellationToken cancellationToken,
                int pageNumber = 1,
                int pageSize = 20) =>
            {
                return (await service.ListAsync(pageNumber, pageSize, cancellationToken)).ToHttpResult();
            })
            .WithName("ListOrgUnits")
            .WithSummary("Lists organisation units within the caller's scope.")
            .ProducesPagedEnvelope<OrgUnitDto>()
            .RequirePermission(PermissionCatalog.OrgUnits.Read);

        group.MapGet("/{id:guid}", async (
                Guid id,
                IOrgUnitService service,
                CancellationToken cancellationToken) =>
            {
                return (await service.GetByIdAsync(id, cancellationToken)).ToHttpResult();
            })
            .WithName("GetOrgUnit")
            .WithSummary("Gets an organisation unit.")
            .ProducesEnvelope<OrgUnitDto>()
            .RequirePermission(PermissionCatalog.OrgUnits.Read);

        group.MapGet("/{id:guid}/children", async (
                Guid id,
                IOrgUnitService service,
                CancellationToken cancellationToken) =>
            {
                return (await service.ListChildrenAsync(id, cancellationToken)).ToHttpResult();
            })
            .WithName("ListOrgUnitChildren")
            .WithSummary("Lists the direct children of a unit.")
            .ProducesEnvelope<IReadOnlyList<OrgUnitDto>>()
            .RequirePermission(PermissionCatalog.OrgUnits.Read);

        group.MapGet("/roots", async (
                IOrgUnitService service,
                CancellationToken cancellationToken) =>
            {
                return (await service.ListChildrenAsync(null, cancellationToken)).ToHttpResult();
            })
            .WithName("ListOrgUnitRoots")
            .WithSummary("Lists the tenant's root units.")
            .ProducesEnvelope<IReadOnlyList<OrgUnitDto>>()
            .RequirePermission(PermissionCatalog.OrgUnits.Read);

        group.MapGet("/{id:guid}/subtree", async (
                Guid id,
                IOrgUnitService service,
                CancellationToken cancellationToken) =>
            {
                return (await service.ListSubtreeAsync(id, cancellationToken)).ToHttpResult();
            })
            .WithName("ListOrgUnitSubtree")
            .WithSummary("Lists a unit and everything beneath it.")
            .ProducesEnvelope<IReadOnlyList<OrgUnitDto>>()
            .RequirePermission(PermissionCatalog.OrgUnits.Read);

        group.MapPost("/", async (
                CreateOrgUnitRequest request,
                IOrgUnitService service,
                CancellationToken cancellationToken) =>
            {
                return (await service.CreateAsync(request, cancellationToken)).ToHttpResult();
            })
            .WithName("CreateOrgUnit")
            .WithSummary("Creates an organisation unit.")
            .WithValidation<CreateOrgUnitRequest>()
            .ProducesEnvelope<OrgUnitDto>(StatusCodes.Status201Created)
            .RequirePermission(PermissionCatalog.OrgUnits.Write);

        group.MapPut("/{id:guid}", async (
                Guid id,
                RenameOrgUnitRequest request,
                IOrgUnitService service,
                CancellationToken cancellationToken) =>
            {
                return (await service.RenameAsync(id, request, cancellationToken)).ToHttpResult();
            })
            .WithName("RenameOrgUnit")
            .WithSummary("Renames a unit, and re-slugs it when a new slug is supplied.")
            .WithDescription(
                "Changing the slug rewrites the hierarchy path of the entire subtree, so it is " +
                "requested explicitly rather than derived from the new name.")
            .WithValidation<RenameOrgUnitRequest>()
            .ProducesEnvelope<OrgUnitDto>()
            .RequirePermission(PermissionCatalog.OrgUnits.Manage);

        group.MapPost("/{id:guid}/move", async (
                Guid id,
                MoveOrgUnitRequest request,
                IOrgUnitService service,
                CancellationToken cancellationToken) =>
            {
                return (await service.MoveAsync(id, request, cancellationToken)).ToHttpResult();
            })
            .WithName("MoveOrgUnit")
            .WithSummary("Moves a unit under a different parent.")
            .WithDescription("Rejected when the target parent sits inside the unit's own subtree.")
            .WithValidation<MoveOrgUnitRequest>()
            .ProducesEnvelope<OrgUnitDto>()
            .RequirePermission(PermissionCatalog.OrgUnits.Manage);

        group.MapDelete("/{id:guid}", async (
                Guid id,
                IOrgUnitService service,
                CancellationToken cancellationToken) =>
            {
                return (await service.DeleteAsync(id, cancellationToken)).ToHttpResult();
            })
            .WithName("DeleteOrgUnit")
            .WithSummary("Deletes a unit that has no children.")
            .ProducesEnvelope()
            .RequirePermission(PermissionCatalog.OrgUnits.Manage);

        return app;
    }
}

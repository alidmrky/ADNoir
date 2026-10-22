using AdNoir.BuildingBlocks.Authorization;
using AdNoir.BuildingBlocks.Http;
using AdNoir.IdentityAndOrg.Application.Tenants;
using AdNoir.IdentityAndOrg.Application.Tenants.Dtos;
using AdNoir.Shared.Contracts.Permissions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AdNoir.IdentityAndOrg.Api.Endpoints;

/// <summary>Tenant endpoints.</summary>
/// <remarks>
/// Handlers do one thing: call the service and convert the envelope. No validation, no error
/// mapping, no status-code decisions - those live in the endpoint filter, the global handler
/// and <see cref="ResponseStatusHttpMap"/> respectively.
/// </remarks>
public static class TenantEndpoints
{
    /// <summary>Maps the tenant endpoints.</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapTenantEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        RouteGroupBuilder group = app
            .MapGroup("/api/v1/tenants")
            .WithTags("Tenants");

        group.MapGet("/", async (
                ITenantService service,
                CancellationToken cancellationToken,
                int pageNumber = 1,
                int pageSize = 20) =>
            {
                return (await service.ListAsync(pageNumber, pageSize, cancellationToken)).ToHttpResult();
            })
            .WithName("ListTenants")
            .WithSummary("Lists tenants.")
            .ProducesPagedEnvelope<TenantDto>()
            .RequirePermission(PermissionCatalog.Tenants.Read);

        group.MapGet("/{id:guid}", async (
                Guid id,
                ITenantService service,
                CancellationToken cancellationToken) =>
            {
                return (await service.GetByIdAsync(id, cancellationToken)).ToHttpResult();
            })
            .WithName("GetTenant")
            .WithSummary("Gets a tenant by identifier.")
            .ProducesEnvelope<TenantDto>()
            .RequirePermission(PermissionCatalog.Tenants.Read);

        group.MapGet("/by-slug/{slug}", async (
                string slug,
                ITenantService service,
                CancellationToken cancellationToken) =>
            {
                return (await service.GetBySlugAsync(slug, cancellationToken)).ToHttpResult();
            })
            .WithName("GetTenantBySlug")
            .WithSummary("Gets a tenant by slug.")
            .ProducesEnvelope<TenantDto>()
            .RequirePermission(PermissionCatalog.Tenants.Read);

        group.MapPost("/", async (
                CreateTenantRequest request,
                ITenantService service,
                CancellationToken cancellationToken) =>
            {
                return (await service.CreateAsync(request, cancellationToken)).ToHttpResult();
            })
            .WithName("CreateTenant")
            .WithSummary("Creates a tenant.")
            .WithValidation<CreateTenantRequest>()
            .ProducesEnvelope<TenantDto>(StatusCodes.Status201Created)
            .RequirePermission(PermissionCatalog.Tenants.Manage);

        group.MapPut("/{id:guid}", async (
                Guid id,
                UpdateTenantRequest request,
                ITenantService service,
                CancellationToken cancellationToken) =>
            {
                return (await service.UpdateAsync(id, request, cancellationToken)).ToHttpResult();
            })
            .WithName("UpdateTenant")
            .WithSummary("Updates a tenant.")
            .WithValidation<UpdateTenantRequest>()
            .ProducesEnvelope<TenantDto>()
            .RequirePermission(PermissionCatalog.Tenants.Manage);

        return app;
    }
}

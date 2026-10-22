using AdNoir.BuildingBlocks.Authorization;
using AdNoir.BuildingBlocks.Http;
using AdNoir.Sample.Application.SampleItems;
using AdNoir.Sample.Application.SampleItems.Dto;
using AdNoir.Shared.Contracts.Permissions;
using AdNoir.Shared.Contracts.Responses;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AdNoir.Sample.Api.Endpoints;

/// <summary>Sample item endpoints (outbox pattern demonstration).</summary>
/// <remarks>
/// Handlers do one thing: call the service and convert the envelope. Validation lives in the
/// endpoint filter, error mapping in the global handler, status codes in
/// <see cref="ResponseStatusHttpMap"/>.
/// </remarks>
public static class SampleItemEndpoints
{
    /// <summary>Maps the sample item endpoints.</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapSampleItemEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        RouteGroupBuilder group = app
            .MapGroup("/api/sample-items")
            .WithTags("SampleItems")
            .RequireAuthorization();

        group.MapPost("/", async (
                CreateSampleItemRequest request,
                ISampleItemService service,
                CancellationToken cancellationToken) =>
            {
                return (await service.CreateAsync(request, cancellationToken)).ToHttpResult();
            })
            .WithName("CreateSampleItem").WithValidation<CreateSampleItemRequest>()
            .WithSummary("Creates a sample item and publishes the created event via the outbox.")
            .RequirePermission(PermissionCatalog.Items.Write)
            .Produces<BaseResponse<SampleItemDto>>(StatusCodes.Status201Created)
            .Produces<BaseResponse>(StatusCodes.Status400BadRequest)
            .Produces<BaseResponse>(StatusCodes.Status401Unauthorized)
            .Produces<BaseResponse>(StatusCodes.Status403Forbidden);

        group.MapGet("/{id:guid}", async (
                Guid id,
                ISampleItemService service,
                CancellationToken cancellationToken) =>
            {
                return (await service.GetAsync(id, cancellationToken)).ToHttpResult();
            })
            .WithName("GetSampleItem")
            .WithSummary("Gets a single sample item by its identifier.")
            .RequirePermission(PermissionCatalog.Items.Read)
            .Produces<BaseResponse<SampleItemDto>>()
            .Produces<BaseResponse>(StatusCodes.Status401Unauthorized)
            .Produces<BaseResponse>(StatusCodes.Status403Forbidden)
            .Produces<BaseResponse>(StatusCodes.Status404NotFound);

        return app;
    }
}


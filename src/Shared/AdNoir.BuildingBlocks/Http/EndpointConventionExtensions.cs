using AdNoir.Shared.Contracts.Responses;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace AdNoir.BuildingBlocks.Http;

/// <summary>
/// Endpoint conventions shared by every service: validation and OpenAPI declarations.
/// </summary>
/// <remarks>
/// Because ProblemDetails was dropped, OpenAPI cannot infer error schemas on its own; each
/// endpoint has to declare them or the Scalar documentation comes out empty.
/// Declaring six statuses by hand on every endpoint would not survive contact with a real
/// codebase, so the repetition is packed into these extensions instead.
/// </remarks>
public static class EndpointConventionExtensions
{
    /// <summary>Validates the request body with FluentValidation before the handler runs.</summary>
    /// <typeparam name="TRequest">The request type to validate.</typeparam>
    /// <param name="builder">The endpoint builder.</param>
    /// <returns>The same builder.</returns>
    public static RouteHandlerBuilder WithValidation<TRequest>(this RouteHandlerBuilder builder)
        where TRequest : class
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddEndpointFilter<ValidationEndpointFilter<TRequest>>();
    }

    /// <summary>
    /// Declares the standard envelope responses for an endpoint that returns a payload:
    /// success plus the failure statuses any authenticated endpoint can produce.
    /// </summary>
    /// <typeparam name="TPayload">The payload type carried by the success response.</typeparam>
    /// <param name="builder">The endpoint builder.</param>
    /// <param name="successStatusCode">The status code returned on success.</param>
    /// <returns>The same builder.</returns>
    public static RouteHandlerBuilder ProducesEnvelope<TPayload>(
        this RouteHandlerBuilder builder,
        int successStatusCode = StatusCodes.Status200OK)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .Produces<BaseResponse<TPayload>>(successStatusCode)
            .ProducesEnvelopeFailures();
    }

    /// <summary>
    /// Declares the standard envelope responses for an endpoint that returns no payload.
    /// </summary>
    /// <param name="builder">The endpoint builder.</param>
    /// <param name="successStatusCode">The status code returned on success.</param>
    /// <returns>The same builder.</returns>
    public static RouteHandlerBuilder ProducesEnvelope(
        this RouteHandlerBuilder builder,
        int successStatusCode = StatusCodes.Status200OK)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .Produces<BaseResponse>(successStatusCode)
            .ProducesEnvelopeFailures();
    }

    /// <summary>
    /// Declares a paged success response plus the standard failures.
    /// </summary>
    /// <typeparam name="TItem">The item type of the page.</typeparam>
    /// <param name="builder">The endpoint builder.</param>
    /// <returns>The same builder.</returns>
    public static RouteHandlerBuilder ProducesPagedEnvelope<TItem>(this RouteHandlerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .Produces<PagedResponse<TItem>>(StatusCodes.Status200OK)
            .ProducesEnvelopeFailures();
    }

    /// <summary>
    /// Declares the failure statuses every endpoint can return. All of them carry
    /// <see cref="BaseResponse"/>, never ProblemDetails.
    /// </summary>
    /// <param name="builder">The endpoint builder.</param>
    /// <returns>The same builder.</returns>
    public static RouteHandlerBuilder ProducesEnvelopeFailures(this RouteHandlerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .Produces<BaseResponse>(StatusCodes.Status400BadRequest)
            .Produces<BaseResponse>(StatusCodes.Status401Unauthorized)
            .Produces<BaseResponse>(StatusCodes.Status403Forbidden)
            .Produces<BaseResponse>(StatusCodes.Status404NotFound)
            .Produces<BaseResponse>(StatusCodes.Status409Conflict)
            .Produces<BaseResponse>(StatusCodes.Status422UnprocessableEntity)
            .Produces<BaseResponse>(StatusCodes.Status500InternalServerError);
    }
}

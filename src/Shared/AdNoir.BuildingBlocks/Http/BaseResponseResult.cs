using System.Reflection;
using AdNoir.BuildingBlocks.Messages;
using AdNoir.Shared.Contracts.Responses;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace AdNoir.BuildingBlocks.Http;

/// <summary>
/// Writes a <see cref="BaseResponse"/> to the HTTP response: enriches it centrally, maps its
/// status to an HTTP status code, and serializes it with the envelope's JSON settings.
/// </summary>
/// <typeparam name="TResponse">
/// The concrete response type. Keeping it generic matters: serializing through the
/// <see cref="BaseResponse"/> base type would silently drop <c>Data</c>, because
/// System.Text.Json writes the declared type's properties, not the runtime type's.
/// </typeparam>
public sealed class BaseResponseResult<TResponse> : IResult, IEndpointMetadataProvider
    where TResponse : BaseResponse
{
    private readonly TResponse _response;

    /// <summary>Initializes a new instance.</summary>
    /// <param name="response">The response to write.</param>
    /// <exception cref="ArgumentNullException"><paramref name="response"/> is null.</exception>
    public BaseResponseResult(TResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        _response = response;
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        IMessageResolver resolver = httpContext.RequestServices.GetRequiredService<IMessageResolver>();
        TResponse enriched = ResponseEnricher.Enrich(_response, httpContext, resolver);

        httpContext.Response.StatusCode = ResponseStatusHttpMap.ToHttpStatusCode(enriched.Status);

        await httpContext.Response.WriteAsJsonAsync(
            enriched,
            AdNoirJsonOptions.Default,
            contentType: "application/json",
            httpContext.RequestAborted);
    }

    /// <summary>
    /// Declares the response shape for OpenAPI. ProblemDetails is not used, so nothing is
    /// inferred automatically and every status has to be stated.
    /// </summary>
    /// <param name="method">The endpoint handler.</param>
    /// <param name="builder">The endpoint builder.</param>
    public static void PopulateMetadata(MethodInfo method, EndpointBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Metadata.Add(new ProducesResponseTypeMetadata(
            StatusCodes.Status200OK,
            typeof(TResponse),
            ["application/json"]));
    }
}

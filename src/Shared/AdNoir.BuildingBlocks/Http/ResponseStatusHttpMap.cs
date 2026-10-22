using AdNoir.Shared.Contracts.Responses;
using Microsoft.AspNetCore.Http;

namespace AdNoir.BuildingBlocks.Http;

/// <summary>
/// The single place where <see cref="ResponseStatus"/> becomes an HTTP status code.
/// Endpoints never choose a status code themselves.
/// </summary>
public static class ResponseStatusHttpMap
{
    /// <summary>Maps an operation outcome to its HTTP status code.</summary>
    /// <param name="status">The outcome.</param>
    /// <returns>The HTTP status code.</returns>
    /// <remarks>
    /// <para>
    /// <see cref="ResponseStatus.NoContent"/> maps to <c>200 OK</c>, not <c>204</c>. A 204
    /// response may not carry a body, and every response - success or
    /// failure - must arrive inside the envelope. Returning 204 would drop the
    /// envelope for exactly one status and force clients to special-case it.
    /// </para>
    /// <para>
    /// <see cref="ResponseStatus.BusinessRuleViolation"/> maps to <c>422</c> rather than
    /// <c>400</c>: the request was well-formed and understood, it was the domain that
    /// refused it. That distinction is what lets a client tell "fix your input" apart from
    /// "this is not allowed right now".
    /// </para>
    /// </remarks>
    public static int ToHttpStatusCode(ResponseStatus status) =>
        status switch
        {
            ResponseStatus.Success => StatusCodes.Status200OK,
            ResponseStatus.Created => StatusCodes.Status201Created,
            ResponseStatus.Accepted => StatusCodes.Status202Accepted,
            ResponseStatus.NoContent => StatusCodes.Status200OK,
            ResponseStatus.ValidationFailed => StatusCodes.Status400BadRequest,
            ResponseStatus.NotFound => StatusCodes.Status404NotFound,
            ResponseStatus.Conflict => StatusCodes.Status409Conflict,
            ResponseStatus.Unauthorized => StatusCodes.Status401Unauthorized,
            ResponseStatus.Forbidden => StatusCodes.Status403Forbidden,
            ResponseStatus.BusinessRuleViolation => StatusCodes.Status422UnprocessableEntity,
            ResponseStatus.ConcurrencyConflict => StatusCodes.Status409Conflict,
            ResponseStatus.RateLimited => StatusCodes.Status429TooManyRequests,
            ResponseStatus.ExternalServiceError => StatusCodes.Status502BadGateway,
            ResponseStatus.UnexpectedError => StatusCodes.Status500InternalServerError,
            ResponseStatus.MethodNotAllowed => StatusCodes.Status405MethodNotAllowed,
            ResponseStatus.UnsupportedMediaType => StatusCodes.Status415UnsupportedMediaType,
            ResponseStatus.ServiceUnavailable => StatusCodes.Status503ServiceUnavailable,
            ResponseStatus.GatewayTimeout => StatusCodes.Status504GatewayTimeout,
            _ => StatusCodes.Status500InternalServerError,
        };
}

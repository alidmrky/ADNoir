namespace AdNoir.Shared.Contracts.Responses;

/// <summary>
/// The outcome of a service operation. This is the single source of truth for what
/// happened; the HTTP status code is derived from it at the boundary, never
/// the other way around.
/// </summary>
public enum ResponseStatus
{
    /// <summary>The operation completed and produced a result.</summary>
    Success = 0,

    /// <summary>A new resource was created.</summary>
    Created = 1,

    /// <summary>The request was accepted for asynchronous processing.</summary>
    Accepted = 2,

    /// <summary>The operation completed and deliberately produced no content.</summary>
    NoContent = 3,

    /// <summary>Input failed validation. Details are carried in <c>Errors</c>, one per field.</summary>
    ValidationFailed = 4,

    /// <summary>The requested resource does not exist, or is not visible to the caller.</summary>
    NotFound = 5,

    /// <summary>The operation conflicts with the current state of the resource.</summary>
    Conflict = 6,

    /// <summary>The caller is not authenticated, or the credentials presented are not valid.</summary>
    Unauthorized = 7,

    /// <summary>The caller is authenticated but lacks the required permission.</summary>
    Forbidden = 8,

    /// <summary>A domain rule rejected the operation.</summary>
    BusinessRuleViolation = 9,

    /// <summary>
    /// Another writer modified the entity first. Produced by the optimistic concurrency
    /// check, where the expected version is part of the update filter.
    /// </summary>
    ConcurrencyConflict = 10,

    /// <summary>The caller exceeded the allowed request rate.</summary>
    RateLimited = 11,

    /// <summary>
    /// A downstream service could not be reached or failed. Note that authorization
    /// deliberately fails closed with this status rather than assuming permission.
    /// </summary>
    ExternalServiceError = 12,

    /// <summary>An unhandled fault. Produced only by the global exception handler.</summary>
    UnexpectedError = 13,

    /// <summary>The HTTP method is not supported by this route.</summary>
    MethodNotAllowed = 14,

    /// <summary>The request content type is not supported.</summary>
    UnsupportedMediaType = 15,

    /// <summary>A required infrastructure dependency is unavailable.</summary>
    ServiceUnavailable = 16,

    /// <summary>A downstream request exceeded its deadline.</summary>
    GatewayTimeout = 17,
}

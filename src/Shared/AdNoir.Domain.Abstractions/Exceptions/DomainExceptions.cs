namespace AdNoir.Domain.Abstractions.Exceptions;

/// <summary>
/// One rejected input field. Mirrors the shape of a validation error without depending on
/// <c>AdNoir.Shared.Contracts</c>, which this project may not reference.
/// </summary>
/// <param name="Field">Property path that failed.</param>
/// <param name="Code">Message catalog key for the failure.</param>
/// <param name="Args">Named values for the catalog entry's placeholders.</param>
public sealed record ValidationFailure(
    string Field,
    string Code,
    IReadOnlyDictionary<string, object?>? Args = null);

/// <summary>
/// A resource does not exist. Thrown only where absence is genuinely exceptional; a lookup
/// that is expected to miss returns <c>BaseResponse.NotFound(...)</c> instead.
/// </summary>
public sealed class NotFoundException : AdNoirException
{
    /// <summary>Initializes a new instance.</summary>
    /// <param name="errorCode">Message catalog key describing what was not found.</param>
    /// <param name="message">Developer-facing message for logs.</param>
    /// <param name="details">Named values for the catalog entry's placeholders.</param>
    public NotFoundException(
        string errorCode,
        string? message = null,
        IReadOnlyDictionary<string, object?>? details = null)
        : base(errorCode, message, innerException: null, details)
    {
    }
}

/// <summary>Input failed validation.</summary>
public sealed class ValidationException : AdNoirException
{
    /// <summary>Initializes a new instance.</summary>
    /// <param name="failures">One entry per rejected field.</param>
    /// <param name="errorCode">Summary message catalog key.</param>
    /// <param name="message">Developer-facing message for logs.</param>
    /// <exception cref="ArgumentNullException"><paramref name="failures"/> is null.</exception>
    public ValidationException(
        IReadOnlyList<ValidationFailure> failures,
        string errorCode,
        string? message = null)
        : base(errorCode, message)
    {
        ArgumentNullException.ThrowIfNull(failures);

        Failures = failures;
    }

    /// <summary>The rejected fields.</summary>
    public IReadOnlyList<ValidationFailure> Failures { get; }
}

/// <summary>The operation conflicts with the current state of the resource.</summary>
public sealed class ConflictException : AdNoirException
{
    /// <summary>Initializes a new instance.</summary>
    /// <param name="errorCode">Message catalog key describing the conflict.</param>
    /// <param name="message">Developer-facing message for logs.</param>
    /// <param name="details">Named values for the catalog entry's placeholders.</param>
    public ConflictException(
        string errorCode,
        string? message = null,
        IReadOnlyDictionary<string, object?>? details = null)
        : base(errorCode, message, innerException: null, details)
    {
    }
}

/// <summary>The caller is authenticated but lacks the required permission.</summary>
public sealed class ForbiddenException : AdNoirException
{
    /// <summary>Initializes a new instance.</summary>
    /// <param name="errorCode">Message catalog key describing the missing permission.</param>
    /// <param name="message">Developer-facing message for logs.</param>
    /// <param name="details">Named values for the catalog entry's placeholders.</param>
    public ForbiddenException(
        string errorCode,
        string? message = null,
        IReadOnlyDictionary<string, object?>? details = null)
        : base(errorCode, message, innerException: null, details)
    {
    }
}

/// <summary>A domain invariant rejected the operation.</summary>
public sealed class BusinessRuleException : AdNoirException
{
    /// <summary>Initializes a new instance.</summary>
    /// <param name="errorCode">Message catalog key naming the violated rule.</param>
    /// <param name="message">Developer-facing message for logs.</param>
    /// <param name="details">Named values for the catalog entry's placeholders.</param>
    public BusinessRuleException(
        string errorCode,
        string? message = null,
        IReadOnlyDictionary<string, object?>? details = null)
        : base(errorCode, message, innerException: null, details)
    {
    }
}

/// <summary>
/// Another writer modified the entity first. Raised by the repository when a versioned
/// update matches no document.
/// </summary>
public sealed class ConcurrencyException : AdNoirException
{
    /// <summary>Initializes a new instance.</summary>
    /// <param name="errorCode">Message catalog key describing the conflict.</param>
    /// <param name="message">Developer-facing message for logs.</param>
    /// <param name="details">Named values for the catalog entry's placeholders.</param>
    public ConcurrencyException(
        string errorCode,
        string? message = null,
        IReadOnlyDictionary<string, object?>? details = null)
        : base(errorCode, message, innerException: null, details)
    {
    }
}

/// <summary>
/// A downstream service could not be reached or failed. Authorization deliberately turns
/// this into a rejected request rather than assuming permission.
/// </summary>
public sealed class ExternalServiceException : AdNoirException
{
    /// <summary>Initializes a new instance.</summary>
    /// <param name="errorCode">Message catalog key describing the failure.</param>
    /// <param name="serviceName">The service that failed, for logs and metrics.</param>
    /// <param name="message">Developer-facing message for logs.</param>
    /// <param name="innerException">The underlying transport or protocol fault.</param>
    public ExternalServiceException(
        string errorCode,
        string serviceName,
        string? message = null,
        Exception? innerException = null)
        : base(errorCode, message, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        ServiceName = serviceName;
    }

    /// <summary>The downstream service that failed.</summary>
    public string ServiceName { get; }
}

namespace AdNoir.Domain.Abstractions.Exceptions;

/// <summary>
/// Base type for faults raised by our own code.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Exceptions are not flow control.</strong> An expected outcome — "user not found",
/// "e-mail already taken" — is returned as a <c>BaseResponse</c> failure, not thrown. These
/// exceptions are for infrastructure faults and for deep domain invariants that a caller
/// cannot reasonably be asked to check first. A single global handler converts them into the
/// response envelope, so service methods carry no try/catch.
/// </para>
/// <para>
/// <strong>Why there is no ResponseStatus property here.</strong> That enum lives
/// in <c>AdNoir.Shared.Contracts</c> and this project is required to reference nothing at
/// all. Rather than break the dependency rule that every other layer rule rests
/// on, the exception type to status mapping lives in the global exception handler — which is
/// already the single place where that mapping is defined. The information is not
/// lost, only located where it does not cost a project reference.
/// </para>
/// <para>
/// Guard clauses use the standard framework types instead of these
/// (<c>ArgumentNullException.ThrowIfNull</c> and friends), and
/// <see cref="NullReferenceException"/>, <see cref="IndexOutOfRangeException"/>,
/// <see cref="AccessViolationException"/>, <see cref="StackOverflowException"/> and
/// <see cref="OutOfMemoryException"/> are never thrown deliberately — an architecture test
/// scans method bodies to enforce that.
/// </para>
/// </remarks>
public abstract class AdNoirException : Exception
{
    /// <summary>Initializes a new instance.</summary>
    /// <param name="errorCode">Message catalog key describing the fault.</param>
    /// <param name="message">Developer-facing message for logs. The user-facing text is resolved from <paramref name="errorCode"/>.</param>
    /// <param name="innerException">The underlying fault, when there is one.</param>
    /// <param name="details">Named values for the catalog entry's placeholders.</param>
    /// <exception cref="ArgumentException"><paramref name="errorCode"/> is null or whitespace.</exception>
    protected AdNoirException(
        string errorCode,
        string? message = null,
        Exception? innerException = null,
        IReadOnlyDictionary<string, object?>? details = null)
        : base(message ?? errorCode, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);

        ErrorCode = errorCode;
        Details = details;
    }

    /// <summary>
    /// Message catalog key, e.g. <c>identity.user.not_found</c>. Resolved to text at the
    /// boundary; never a literal written into code.
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>Named values for the placeholders of <see cref="ErrorCode"/>.</summary>
    public IReadOnlyDictionary<string, object?>? Details { get; }
}

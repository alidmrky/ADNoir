using AdNoir.Shared.Contracts.Messages;

namespace AdNoir.Shared.Contracts.Responses;

/// <summary>
/// The envelope for an operation that produces a value.
/// </summary>
/// <typeparam name="T">
/// The payload type. Always a DTO — returning a domain entity is forbidden and enforced by
/// an architecture test, otherwise the database schema silently becomes the API contract
/// and fields like <c>IsDeleted</c>, <c>TenantId</c> or a password hash leak.
/// </typeparam>
/// <remarks>
/// The factory methods deliberately shadow the non-generic ones with <c>new</c>: both types
/// need a <c>NotFound</c>, and without the modifier the inherited member would be hidden
/// accidentally rather than on purpose.
/// </remarks>
public record BaseResponse<T> : BaseResponse
{
    /// <summary>Initializes a new instance. Not public by design — use the factory methods.</summary>
    protected BaseResponse()
    {
    }

    /// <summary>
    /// The payload. Null on failure, and omitted from JSON when null so that failures do not
    /// carry a meaningless <c>"data": null</c>.
    /// </summary>
    public T? Data { get; init; }

    // ---------------------------------------------------------------------------------
    // Factories — success
    // ---------------------------------------------------------------------------------

    /// <summary>Creates a successful response carrying a value.</summary>
    /// <param name="data">The payload.</param>
    /// <param name="messageCode">Catalog key; defaults to <see cref="MessageCodes.Common.Success"/>.</param>
    /// <param name="args">Optional named placeholder values.</param>
    /// <returns>A response with <see cref="ResponseStatus.Success"/>.</returns>
    public static BaseResponse<T> Success(
        T data,
        string messageCode = MessageCodes.Common.Success,
        params (string Key, object? Value)[] args) =>
        new()
        {
            Status = ResponseStatus.Success,
            MessageCode = messageCode,
            MessageArgs = ResponseArgs.From(args),
            Data = data,
        };

    /// <summary>Creates a response indicating the value was newly created.</summary>
    /// <param name="data">The created payload.</param>
    /// <param name="messageCode">Catalog key; defaults to <see cref="MessageCodes.Common.Created"/>.</param>
    /// <param name="args">Optional named placeholder values.</param>
    /// <returns>A response with <see cref="ResponseStatus.Created"/>.</returns>
    public static BaseResponse<T> Created(
        T data,
        string messageCode = MessageCodes.Common.Created,
        params (string Key, object? Value)[] args) =>
        new()
        {
            Status = ResponseStatus.Created,
            MessageCode = messageCode,
            MessageArgs = ResponseArgs.From(args),
            Data = data,
        };

    /// <summary>Creates a response indicating the request was accepted for later processing.</summary>
    /// <param name="data">Optional payload describing the accepted work.</param>
    /// <param name="messageCode">Catalog key; defaults to <see cref="MessageCodes.Common.Accepted"/>.</param>
    /// <param name="args">Optional named placeholder values.</param>
    /// <returns>A response with <see cref="ResponseStatus.Accepted"/>.</returns>
    public static BaseResponse<T> Accepted(
        T? data = default,
        string messageCode = MessageCodes.Common.Accepted,
        params (string Key, object? Value)[] args) =>
        new()
        {
            Status = ResponseStatus.Accepted,
            MessageCode = messageCode,
            MessageArgs = ResponseArgs.From(args),
            Data = data,
        };

    // ---------------------------------------------------------------------------------
    // Factories — failure
    // ---------------------------------------------------------------------------------

    /// <summary>Creates a response indicating the resource was not found.</summary>
    /// <param name="messageCode">Catalog key describing what was not found.</param>
    /// <param name="args">Optional named placeholder values.</param>
    /// <returns>A response with <see cref="ResponseStatus.NotFound"/>.</returns>
    public static new BaseResponse<T> NotFound(
        string messageCode = MessageCodes.Common.NotFound,
        params (string Key, object? Value)[] args) =>
        Failure(ResponseStatus.NotFound, messageCode, args);

    /// <summary>Creates a response indicating input validation failed.</summary>
    /// <param name="errors">One message per rejected field.</param>
    /// <param name="messageCode">Summary catalog key.</param>
    /// <returns>A response with <see cref="ResponseStatus.ValidationFailed"/>.</returns>
    public static new BaseResponse<T> ValidationFailed(
        IReadOnlyList<ResponseMessage> errors,
        string messageCode = MessageCodes.Common.ValidationFailed) =>
        new()
        {
            Status = ResponseStatus.ValidationFailed,
            MessageCode = messageCode,
            Errors = errors,
        };

    /// <summary>Creates a response indicating a conflict with the current resource state.</summary>
    /// <param name="messageCode">Catalog key describing the conflict.</param>
    /// <param name="args">Optional named placeholder values.</param>
    /// <returns>A response with <see cref="ResponseStatus.Conflict"/>.</returns>
    public static new BaseResponse<T> Conflict(
        string messageCode = MessageCodes.Common.Conflict,
        params (string Key, object? Value)[] args) =>
        Failure(ResponseStatus.Conflict, messageCode, args);

    /// <summary>Creates a response indicating the caller is not authenticated.</summary>
    /// <param name="messageCode">Catalog key describing why.</param>
    /// <param name="args">Optional named placeholder values.</param>
    /// <returns>A response with <see cref="ResponseStatus.Unauthorized"/>.</returns>
    public static new BaseResponse<T> Unauthorized(
        string messageCode = MessageCodes.Common.Unauthorized,
        params (string Key, object? Value)[] args) =>
        Failure(ResponseStatus.Unauthorized, messageCode, args);

    /// <summary>Creates a response indicating the caller lacks the required permission.</summary>
    /// <param name="messageCode">Catalog key describing the missing permission.</param>
    /// <param name="args">Optional named placeholder values.</param>
    /// <returns>A response with <see cref="ResponseStatus.Forbidden"/>.</returns>
    public static new BaseResponse<T> Forbidden(
        string messageCode = MessageCodes.Common.Forbidden,
        params (string Key, object? Value)[] args) =>
        Failure(ResponseStatus.Forbidden, messageCode, args);

    /// <summary>Creates a response indicating a domain rule rejected the operation.</summary>
    /// <param name="messageCode">Catalog key naming the violated rule.</param>
    /// <param name="args">Optional named placeholder values.</param>
    /// <returns>A response with <see cref="ResponseStatus.BusinessRuleViolation"/>.</returns>
    public static new BaseResponse<T> BusinessRuleViolation(
        string messageCode = MessageCodes.Common.BusinessRuleViolation,
        params (string Key, object? Value)[] args) =>
        Failure(ResponseStatus.BusinessRuleViolation, messageCode, args);

    /// <summary>Creates a response indicating another writer modified the entity first.</summary>
    /// <param name="messageCode">Catalog key describing the conflict.</param>
    /// <param name="args">Optional named placeholder values.</param>
    /// <returns>A response with <see cref="ResponseStatus.ConcurrencyConflict"/>.</returns>
    public static new BaseResponse<T> ConcurrencyConflict(
        string messageCode = MessageCodes.Common.ConcurrencyConflict,
        params (string Key, object? Value)[] args) =>
        Failure(ResponseStatus.ConcurrencyConflict, messageCode, args);

    /// <summary>Creates a failure response with an explicit status.</summary>
    /// <param name="status">The failure status.</param>
    /// <param name="messageCode">Catalog key describing the failure.</param>
    /// <param name="args">Optional named placeholder values.</param>
    /// <returns>A failure response with no payload.</returns>
    public static new BaseResponse<T> Failure(
        ResponseStatus status,
        string messageCode,
        params (string Key, object? Value)[] args)
    {
        IReadOnlyDictionary<string, object?>? arguments = ResponseArgs.From(args);

        return new BaseResponse<T>
        {
            Status = status,
            MessageCode = messageCode,
            MessageArgs = arguments,
            Errors = [new ResponseMessage(messageCode, MessageSeverity.Error, Args: arguments)],
        };
    }

    /// <summary>
    /// Carries a failure from a lower layer upwards, changing only the payload type.
    /// Status, codes, arguments, errors and warnings are preserved, so no diagnostic
    /// information is lost on the way up.
    /// </summary>
    /// <param name="source">The failed response to propagate.</param>
    /// <returns>A failure response of this payload type.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="source"/> is a success.</exception>
    public static BaseResponse<T> Propagate(BaseResponse source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.IsSuccess)
        {
            throw new InvalidOperationException(
                "Propagate carries failures only; a successful response has a payload that cannot be retyped.");
        }

        return new BaseResponse<T>
        {
            Status = source.Status,
            MessageCode = source.MessageCode,
            Message = source.Message,
            MessageArgs = source.MessageArgs,
            Errors = source.Errors,
            Warnings = source.Warnings,
            CorrelationId = source.CorrelationId,
            TraceId = source.TraceId,
            Metadata = source.Metadata,
        };
    }

    // ---------------------------------------------------------------------------------
    // Functional composition
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Transforms the payload when successful; carries the failure through unchanged otherwise.
    /// </summary>
    /// <typeparam name="TOut">The transformed payload type.</typeparam>
    /// <param name="mapper">Transformation applied to the payload.</param>
    /// <returns>A response carrying the transformed payload, or the original failure.</returns>
    public BaseResponse<TOut> Map<TOut>(Func<T, TOut> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);

        return IsSuccess && Data is not null
            ? BaseResponse<TOut>.Success(mapper(Data), MessageCode)
            : BaseResponse<TOut>.Propagate(this);
    }

    /// <summary>
    /// Chains another operation that itself returns an envelope. The first failure short-circuits.
    /// </summary>
    /// <typeparam name="TOut">The payload type of the next step.</typeparam>
    /// <param name="binder">The next step.</param>
    /// <returns>The next step's response, or the original failure.</returns>
    public BaseResponse<TOut> Bind<TOut>(Func<T, BaseResponse<TOut>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);

        return IsSuccess && Data is not null
            ? binder(Data)
            : BaseResponse<TOut>.Propagate(this);
    }

    /// <summary>
    /// Turns a successful response into a failure when the payload fails a check.
    /// </summary>
    /// <param name="predicate">Check the payload must satisfy.</param>
    /// <param name="messageCode">Catalog key used when the check fails.</param>
    /// <param name="status">Status used when the check fails.</param>
    /// <param name="args">Optional named placeholder values.</param>
    /// <returns>This response, or a failure when the check does not hold.</returns>
    public BaseResponse<T> Ensure(
        Func<T, bool> predicate,
        string messageCode,
        ResponseStatus status = ResponseStatus.BusinessRuleViolation,
        params (string Key, object? Value)[] args)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        if (!IsSuccess || Data is null)
        {
            return this;
        }

        return predicate(Data) ? this : Failure(status, messageCode, args);
    }

    /// <summary>
    /// Runs a side effect on the payload when successful and returns this response unchanged.
    /// </summary>
    /// <param name="action">Side effect to run.</param>
    /// <returns>This response.</returns>
    public BaseResponse<T> Tap(Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (IsSuccess && Data is not null)
        {
            action(Data);
        }

        return this;
    }

    /// <summary>Collapses the envelope into a single value.</summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="onSuccess">Applied to the payload when successful.</param>
    /// <param name="onFailure">Applied to this response when it is a failure.</param>
    /// <returns>The value produced by whichever branch ran.</returns>
    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<BaseResponse<T>, TResult> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return IsSuccess && Data is not null ? onSuccess(Data) : onFailure(this);
    }

    // ---------------------------------------------------------------------------------
    // Boundary helpers
    // ---------------------------------------------------------------------------------

    /// <inheritdoc />
    public override BaseResponse<T> WithContext(string? correlationId, string? traceId) =>
        this with { CorrelationId = correlationId, TraceId = traceId };

    /// <inheritdoc />
    public override BaseResponse<T> WithResolvedMessages(
        string? summaryText,
        Func<ResponseMessage, string?> resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);

        return this with
        {
            Message = summaryText,
            Errors = [.. Errors.Select(e => e.WithResolvedText(resolver(e)))],
            Warnings = [.. Warnings.Select(w => w.WithResolvedText(resolver(w)))],
        };
    }

    /// <inheritdoc />
    public override BaseResponse<T> WithWarnings(params ResponseMessage[] warnings) =>
        this with { Warnings = [.. Warnings, .. warnings] };

    /// <summary>
    /// Wraps a bare value in a successful envelope, so simple methods can write
    /// <c>return dto;</c>.
    /// </summary>
    /// <param name="value">The payload.</param>
    /// <remarks>
    /// This conversion always produces <see cref="ResponseStatus.Success"/>.
    /// <c>Created</c>, <c>Accepted</c> and <c>NoContent</c> must be stated explicitly through
    /// their factories, because an implicit conversion cannot know which one was meant.
    /// </remarks>
    public static implicit operator BaseResponse<T>(T value) => Success(value);
}

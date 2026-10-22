using AdNoir.Shared.Contracts.Messages;

namespace AdNoir.Shared.Contracts.Responses;

/// <summary>
/// The envelope every service operation returns. There is no exception to this contract and
/// it is enforced by an architecture test. ProblemDetails is deliberately not
/// used anywhere: 404 and 500 come back in this same envelope.
/// </summary>
/// <remarks>
/// <para>
/// Declared as a <c>record</c> rather than a plain <c>class</c>. The type is immutable with
/// init-only members, while <see cref="CorrelationId"/>,
/// <see cref="TraceId"/> and <see cref="Message"/> are filled in centrally at the boundary.
/// Those two requirements need a copy operation that preserves the runtime type —
/// <c>with</c> on a record does exactly that through its virtual clone member, so a
/// <c>BaseResponse&lt;UserDto&gt;</c> stays a <c>BaseResponse&lt;UserDto&gt;</c> after enrichment.
/// A plain class would have forced either mutable setters or a hand-written polymorphic
/// clone.
/// </para>
/// <para>
/// Instances are produced only through the factory methods; the constructor is not public.
/// </para>
/// </remarks>
public record BaseResponse
{
    /// <summary>Initializes a new instance. Not public by design — use the factory methods.</summary>
    protected BaseResponse()
    {
    }

    /// <summary>
    /// Whether the operation succeeded. Derived from <see cref="Status"/> rather than stored,
    /// which makes the contradictory combination "IsSuccess = true, Status = NotFound"
    /// unrepresentable instead of merely discouraged.
    /// </summary>
    public bool IsSuccess =>
        Status is ResponseStatus.Success
            or ResponseStatus.Created
            or ResponseStatus.Accepted
            or ResponseStatus.NoContent;

    /// <summary>The outcome of the operation.</summary>
    public ResponseStatus Status { get; init; }

    /// <summary>
    /// Summary catalog key for this response, e.g. <c>identity.user.not_found</c>.
    /// Always a <see cref="MessageCodes"/> constant, never a literal.
    /// </summary>
    public string MessageCode { get; init; } = MessageCodes.Common.Success;

    /// <summary>
    /// Text resolved from the message catalog for the caller's culture. Left null by service
    /// code and filled in centrally when the response is written.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>Values for the named placeholders of <see cref="MessageCode"/>.</summary>
    public IReadOnlyDictionary<string, object?>? MessageArgs { get; init; }

    /// <summary>Detailed errors. Empty on success.</summary>
    public IReadOnlyList<ResponseMessage> Errors { get; init; } = [];

    /// <summary>Detailed warnings. May be present even on success.</summary>
    public IReadOnlyList<ResponseMessage> Warnings { get; init; } = [];

    /// <summary>Correlation identifier, filled in centrally at the boundary.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>W3C trace identifier, filled in centrally at the boundary.</summary>
    public string? TraceId { get; init; }

    /// <summary>When the response was produced, in UTC.</summary>
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Free-form extra data. Keep this empty unless there is genuinely no typed home for the
    /// value. This field is prone to becoming a junk drawer, and anything with a typed
    /// equivalent — pagination above all — belongs in that type instead.
    /// </summary>
    public IReadOnlyDictionary<string, object?>? Metadata { get; init; }

    // ---------------------------------------------------------------------------------
    // Factories — success
    // ---------------------------------------------------------------------------------

    /// <summary>Creates a successful response carrying no value.</summary>
    /// <param name="messageCode">Catalog key; defaults to <see cref="MessageCodes.Common.Success"/>.</param>
    /// <param name="args">Optional named placeholder values.</param>
    /// <returns>A response with <see cref="ResponseStatus.Success"/>.</returns>
    public static BaseResponse Success(
        string messageCode = MessageCodes.Common.Success,
        params (string Key, object? Value)[] args) =>
        new()
        {
            Status = ResponseStatus.Success,
            MessageCode = messageCode,
            MessageArgs = ResponseArgs.From(args),
        };

    /// <summary>Creates a response indicating a resource was created.</summary>
    /// <param name="messageCode">Catalog key; defaults to <see cref="MessageCodes.Common.Created"/>.</param>
    /// <param name="args">Optional named placeholder values.</param>
    /// <returns>A response with <see cref="ResponseStatus.Created"/>.</returns>
    public static BaseResponse Created(
        string messageCode = MessageCodes.Common.Created,
        params (string Key, object? Value)[] args) =>
        new()
        {
            Status = ResponseStatus.Created,
            MessageCode = messageCode,
            MessageArgs = ResponseArgs.From(args),
        };

    /// <summary>Creates a response indicating the request was accepted for later processing.</summary>
    /// <param name="messageCode">Catalog key; defaults to <see cref="MessageCodes.Common.Accepted"/>.</param>
    /// <param name="args">Optional named placeholder values.</param>
    /// <returns>A response with <see cref="ResponseStatus.Accepted"/>.</returns>
    public static BaseResponse Accepted(
        string messageCode = MessageCodes.Common.Accepted,
        params (string Key, object? Value)[] args) =>
        new()
        {
            Status = ResponseStatus.Accepted,
            MessageCode = messageCode,
            MessageArgs = ResponseArgs.From(args),
        };

    /// <summary>Creates a successful response that deliberately carries no content.</summary>
    /// <param name="messageCode">Catalog key; defaults to <see cref="MessageCodes.Common.NoContent"/>.</param>
    /// <param name="args">Optional named placeholder values.</param>
    /// <returns>A response with <see cref="ResponseStatus.NoContent"/>.</returns>
    public static BaseResponse NoContent(
        string messageCode = MessageCodes.Common.NoContent,
        params (string Key, object? Value)[] args) =>
        new()
        {
            Status = ResponseStatus.NoContent,
            MessageCode = messageCode,
            MessageArgs = ResponseArgs.From(args),
        };

    // ---------------------------------------------------------------------------------
    // Factories — failure
    // ---------------------------------------------------------------------------------

    /// <summary>Creates a response indicating the resource was not found.</summary>
    /// <param name="messageCode">Catalog key describing what was not found.</param>
    /// <param name="args">Optional named placeholder values.</param>
    /// <returns>A response with <see cref="ResponseStatus.NotFound"/>.</returns>
    public static BaseResponse NotFound(
        string messageCode = MessageCodes.Common.NotFound,
        params (string Key, object? Value)[] args) =>
        Failure(ResponseStatus.NotFound, messageCode, args);

    /// <summary>Creates a response indicating input validation failed.</summary>
    /// <param name="errors">One message per rejected field.</param>
    /// <param name="messageCode">Summary catalog key.</param>
    /// <returns>A response with <see cref="ResponseStatus.ValidationFailed"/>.</returns>
    public static BaseResponse ValidationFailed(
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
    public static BaseResponse Conflict(
        string messageCode = MessageCodes.Common.Conflict,
        params (string Key, object? Value)[] args) =>
        Failure(ResponseStatus.Conflict, messageCode, args);

    /// <summary>Creates a response indicating the caller is not authenticated.</summary>
    /// <param name="messageCode">Catalog key describing why.</param>
    /// <param name="args">Optional named placeholder values.</param>
    /// <returns>A response with <see cref="ResponseStatus.Unauthorized"/>.</returns>
    public static BaseResponse Unauthorized(
        string messageCode = MessageCodes.Common.Unauthorized,
        params (string Key, object? Value)[] args) =>
        Failure(ResponseStatus.Unauthorized, messageCode, args);

    /// <summary>Creates a response indicating the caller lacks the required permission.</summary>
    /// <param name="messageCode">Catalog key describing the missing permission.</param>
    /// <param name="args">Optional named placeholder values.</param>
    /// <returns>A response with <see cref="ResponseStatus.Forbidden"/>.</returns>
    public static BaseResponse Forbidden(
        string messageCode = MessageCodes.Common.Forbidden,
        params (string Key, object? Value)[] args) =>
        Failure(ResponseStatus.Forbidden, messageCode, args);

    /// <summary>Creates a response indicating a domain rule rejected the operation.</summary>
    /// <param name="messageCode">Catalog key naming the violated rule.</param>
    /// <param name="args">Optional named placeholder values.</param>
    /// <returns>A response with <see cref="ResponseStatus.BusinessRuleViolation"/>.</returns>
    public static BaseResponse BusinessRuleViolation(
        string messageCode = MessageCodes.Common.BusinessRuleViolation,
        params (string Key, object? Value)[] args) =>
        Failure(ResponseStatus.BusinessRuleViolation, messageCode, args);

    /// <summary>Creates a response indicating another writer modified the entity first.</summary>
    /// <param name="messageCode">Catalog key describing the conflict.</param>
    /// <param name="args">Optional named placeholder values.</param>
    /// <returns>A response with <see cref="ResponseStatus.ConcurrencyConflict"/>.</returns>
    public static BaseResponse ConcurrencyConflict(
        string messageCode = MessageCodes.Common.ConcurrencyConflict,
        params (string Key, object? Value)[] args) =>
        Failure(ResponseStatus.ConcurrencyConflict, messageCode, args);

    /// <summary>Creates a failure response with an explicit status.</summary>
    /// <param name="status">The failure status.</param>
    /// <param name="messageCode">Catalog key describing the failure.</param>
    /// <param name="args">Optional named placeholder values.</param>
    /// <returns>A failure response.</returns>
    public static BaseResponse Failure(
        ResponseStatus status,
        string messageCode,
        params (string Key, object? Value)[] args)
    {
        IReadOnlyDictionary<string, object?>? arguments = ResponseArgs.From(args);

        return new BaseResponse
        {
            Status = status,
            MessageCode = messageCode,
            MessageArgs = arguments,
            Errors = [new ResponseMessage(messageCode, MessageSeverity.Error, Args: arguments)],
        };
    }

    // ---------------------------------------------------------------------------------
    // Boundary helpers — used by the response writer, not by service code
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Returns a copy carrying the ambient correlation and trace identifiers. Called once,
    /// centrally, when the response is written.
    /// </summary>
    /// <param name="correlationId">Correlation identifier for the current request.</param>
    /// <param name="traceId">W3C trace identifier for the current request.</param>
    /// <returns>A copy of this response with the identifiers set.</returns>
    public virtual BaseResponse WithContext(string? correlationId, string? traceId) =>
        this with { CorrelationId = correlationId, TraceId = traceId };

    /// <summary>
    /// Returns a copy carrying catalog text resolved for the caller's culture, on the summary
    /// message and on every error and warning. Called centrally at the boundary.
    /// </summary>
    /// <param name="summaryText">Text resolved for <see cref="MessageCode"/>.</param>
    /// <param name="resolver">Resolves the text for a detail message's code.</param>
    /// <returns>A copy of this response with resolved text applied.</returns>
    public virtual BaseResponse WithResolvedMessages(
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

    /// <summary>Adds warnings to a response without changing its status.</summary>
    /// <param name="warnings">Warnings to append.</param>
    /// <returns>A copy of this response with the warnings appended.</returns>
    public virtual BaseResponse WithWarnings(params ResponseMessage[] warnings) =>
        this with { Warnings = [.. Warnings, .. warnings] };
}

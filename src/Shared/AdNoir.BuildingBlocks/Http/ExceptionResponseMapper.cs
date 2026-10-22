using AdNoir.Domain.Abstractions.Exceptions;
using AdNoir.Shared.Contracts.Messages;
using AdNoir.Shared.Contracts.Responses;
using Microsoft.AspNetCore.Http;

namespace AdNoir.BuildingBlocks.Http;

/// <summary>
/// Converts an exception into the response envelope.
/// </summary>
/// <remarks>
/// <para>
/// This is also where an <see cref="AdNoirException"/> acquires its
/// <see cref="ResponseStatus"/>. The exception hierarchy itself cannot carry the enum:
/// it lives in <c>AdNoir.Domain.Abstractions</c>, which references nothing, while the enum
/// lives in <c>AdNoir.Shared.Contracts</c>. Mapping here keeps the layer rule intact and
/// puts the mapping in the same single location used for status translation.
/// </para>
/// <para>
/// Unrecognised exceptions become <see cref="ResponseStatus.UnexpectedError"/> with a generic
/// message code. Internal details are deliberately not echoed to the caller; they go to the
/// log, which is where they are useful and where they are not a disclosure risk.
/// </para>
/// </remarks>
public static class ExceptionResponseMapper
{
    /// <summary>Maps an exception to the status its type represents.</summary>
    /// <param name="exception">The exception.</param>
    /// <returns>The matching outcome.</returns>
    public static ResponseStatus ToStatus(Exception exception) =>
        exception switch
        {
            BadHttpRequestException { StatusCode: StatusCodes.Status400BadRequest } => ResponseStatus.ValidationFailed,
            BadHttpRequestException { StatusCode: StatusCodes.Status415UnsupportedMediaType } => ResponseStatus.UnsupportedMediaType,
            NotFoundException => ResponseStatus.NotFound,
            ValidationException => ResponseStatus.ValidationFailed,
            ConflictException => ResponseStatus.Conflict,
            ForbiddenException => ResponseStatus.Forbidden,
            BusinessRuleException => ResponseStatus.BusinessRuleViolation,
            ConcurrencyException => ResponseStatus.ConcurrencyConflict,
            ExternalServiceException => ResponseStatus.ExternalServiceError,
            OperationCanceledException => ResponseStatus.UnexpectedError,
            _ => ResponseStatus.UnexpectedError,
        };

    /// <summary>Builds the response envelope for an exception.</summary>
    /// <param name="exception">The exception.</param>
    /// <returns>A failure response carrying the exception's message code and details.</returns>
    public static BaseResponse ToResponse(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            BadHttpRequestException { StatusCode: StatusCodes.Status400BadRequest } =>
                Build(ResponseStatus.ValidationFailed, MessageCodes.Common.ValidationFailed, details: null),
            BadHttpRequestException { StatusCode: StatusCodes.Status415UnsupportedMediaType } =>
                Build(ResponseStatus.UnsupportedMediaType, MessageCodes.Gateway.UnsupportedMediaType, details: null),
            ValidationException validation => BaseResponse.ValidationFailed(
                [.. validation.Failures.Select(f => new ResponseMessage(
                    f.Code,
                    MessageSeverity.Error,
                    Field: f.Field,
                    Args: f.Args))],
                validation.ErrorCode),

            AdNoirException known => Build(ToStatus(known), known.ErrorCode, known.Details),

            _ => Build(ResponseStatus.UnexpectedError, MessageCodes.Common.UnexpectedError, details: null),
        };
    }

    private static BaseResponse Build(
        ResponseStatus status,
        string messageCode,
        IReadOnlyDictionary<string, object?>? details)
    {
        BaseResponse response = BaseResponse.Failure(status, messageCode);

        return details is null
            ? response
            : response with
            {
                MessageArgs = details,
                Errors = [new ResponseMessage(messageCode, MessageSeverity.Error, Args: details)],
            };
    }
}

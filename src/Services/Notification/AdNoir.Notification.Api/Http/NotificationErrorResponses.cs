using AdNoir.BuildingBlocks.Http;
using AdNoir.Shared.Contracts.Messages;
using AdNoir.Shared.Contracts.Responses;

namespace AdNoir.Notification.Api.Http;

/// <summary>Converts empty notification HTTP errors into the shared envelope.</summary>
public static class NotificationErrorResponses
{
    /// <summary>Writes a catalog-backed response for a notification status.</summary>
    public static Task WriteAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        (ResponseStatus status, string code) = context.Response.StatusCode switch
        {
            400 => (ResponseStatus.ValidationFailed, MessageCodes.Common.ValidationFailed),
            401 => (ResponseStatus.Unauthorized, MessageCodes.Common.Unauthorized),
            403 => (ResponseStatus.Forbidden, MessageCodes.Common.Forbidden),
            404 => (ResponseStatus.NotFound, MessageCodes.Common.NotFound),
            405 => (ResponseStatus.MethodNotAllowed, MessageCodes.Gateway.MethodNotAllowed),
            415 => (ResponseStatus.UnsupportedMediaType, MessageCodes.Gateway.UnsupportedMediaType),
            429 => (ResponseStatus.RateLimited, MessageCodes.Common.RateLimited),
            502 => (ResponseStatus.ExternalServiceError, MessageCodes.Common.ExternalServiceError),
            503 => (ResponseStatus.ServiceUnavailable, MessageCodes.Common.ExternalServiceError),
            504 => (ResponseStatus.GatewayTimeout, MessageCodes.Common.ExternalServiceError),
            _ => (ResponseStatus.UnexpectedError, MessageCodes.Common.UnexpectedError),
        };
        return BaseResponse.Failure(status, code).ToHttpResult().ExecuteAsync(context);
    }
}

using AdNoir.BuildingBlocks.Http;
using AdNoir.Shared.Contracts.Messages;
using AdNoir.Shared.Contracts.Responses;
using Microsoft.AspNetCore.Diagnostics;
using StackExchange.Redis;

namespace AdNoir.Gateway.Http;

/// <summary>Fails closed when the distributed request budget cannot be checked.</summary>
public sealed partial class GatewayInfrastructureExceptionHandler(ILogger<GatewayInfrastructureExceptionHandler> logger) : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);
        if (exception is not RedisException)
        {
            return false;
        }
        cancellationToken.ThrowIfCancellationRequested();
        LogRedisUnavailable(logger, exception);
        await BaseResponse.Failure(ResponseStatus.ServiceUnavailable, MessageCodes.Common.ExternalServiceError)
            .ToHttpResult().ExecuteAsync(httpContext);
        return true;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Distributed gateway rate limiter is unavailable.")]
    private static partial void LogRedisUnavailable(ILogger logger, Exception exception);
}

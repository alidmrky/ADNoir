using AdNoir.BuildingBlocks.Messages;
using AdNoir.Domain.Abstractions.Exceptions;
using AdNoir.Shared.Contracts.Responses;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AdNoir.BuildingBlocks.Http;

/// <summary>
/// The single exception handler for the application. Because it exists, service
/// methods contain no try/catch: an unexpected fault rises to here and leaves as an envelope.
/// </summary>
/// <remarks>
/// ProblemDetails is not produced. A 500 arrives in the same <c>BaseResponse</c> shape as
/// every other response, so a client needs one parser, not two.
/// </remarks>
public sealed class GlobalExceptionHandler(
    IMessageResolver messageResolver,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    private readonly IMessageResolver _messageResolver = messageResolver;
    private readonly ILogger<GlobalExceptionHandler> _logger = logger;

    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        // A cancelled request is the client hanging up, not a fault. Writing a response would
        // fail anyway, and logging it as an error fills the log with noise during deployments.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            return false;
        }

        BaseResponse response = ExceptionResponseMapper.ToResponse(exception);
        string correlationId = ResponseEnricher.ResolveCorrelationId(httpContext);

        LogException(exception, response, correlationId, httpContext);

        BaseResponse enriched = ResponseEnricher.Enrich(response, httpContext, _messageResolver);

        httpContext.Response.StatusCode = ResponseStatusHttpMap.ToHttpStatusCode(enriched.Status);

        await httpContext.Response.WriteAsJsonAsync(
            enriched,
            AdNoirJsonOptions.Default,
            contentType: "application/json",
            cancellationToken);

        return true;
    }

    private void LogException(
        Exception exception,
        BaseResponse response,
        string correlationId,
        HttpContext httpContext)
    {
        if (exception is BadHttpRequestException && response.Status != ResponseStatus.UnexpectedError)
        {
            // Parser exception messages can contain submitted values; do not log the exception.
            _logger.LogWarning("Rejected request binding for {Method} {Path}: {MessageCode} (correlation {CorrelationId})",
                httpContext.Request.Method, httpContext.Request.Path.Value, response.MessageCode, correlationId);
            return;
        }

        // Expected faults are warnings: they say something about the request, not about the
        // system. Only genuinely unexpected ones deserve an error, otherwise the error level
        // stops meaning anything.
        if (exception is AdNoirException)
        {
            _logger.LogWarning(
                exception,
                "Handled {ExceptionType} for {Method} {Path}: {MessageCode} (correlation {CorrelationId})",
                exception.GetType().Name,
                httpContext.Request.Method,
                httpContext.Request.Path.Value,
                response.MessageCode,
                correlationId);

            return;
        }

        _logger.LogError(
            exception,
            "Unhandled exception for {Method} {Path} (correlation {CorrelationId})",
            httpContext.Request.Method,
            httpContext.Request.Path.Value,
            correlationId);
    }
}

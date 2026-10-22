using System.Diagnostics;
using System.Globalization;
using AdNoir.BuildingBlocks.Messages;
using AdNoir.Shared.Contracts.Responses;
using Microsoft.AspNetCore.Http;

namespace AdNoir.BuildingBlocks.Http;

/// <summary>
/// Fills in the parts of a response that service code deliberately leaves empty:
/// correlation id, trace id and the resolved message text.
/// </summary>
/// <remarks>
/// Doing this in one place is the whole point. If every service method had to carry the
/// correlation id and look up its own text, the two would drift apart within a month, and
/// the drift would show up as a blank message on a user's screen.
/// </remarks>
public static class ResponseEnricher
{
    /// <summary>The request header carrying an inbound correlation id.</summary>
    public const string CorrelationIdHeader = "X-Correlation-Id";

    /// <summary>Enriches a response using the ambient request context.</summary>
    /// <typeparam name="TResponse">The concrete response type, preserved so the payload is serialized.</typeparam>
    /// <param name="response">The response produced by the service.</param>
    /// <param name="httpContext">The current request.</param>
    /// <param name="resolver">The message catalog resolver.</param>
    /// <returns>The enriched response, of the same runtime type.</returns>
    public static TResponse Enrich<TResponse>(
        TResponse response,
        HttpContext httpContext,
        IMessageResolver resolver)
        where TResponse : BaseResponse
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(resolver);

        string correlationId = ResolveCorrelationId(httpContext);
        string? traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        CultureInfo culture = CultureInfo.CurrentUICulture;

        BaseResponse withContext = response.WithContext(correlationId, traceId);

        BaseResponse resolved = withContext.WithResolvedMessages(
            resolver.Resolve(response.MessageCode, response.MessageArgs, culture),
            message => resolver.Resolve(message.Code, message.Args, culture));

        // WithContext and WithResolvedMessages are virtual and implemented with `with`, so the
        // runtime type is preserved through both copies; the cast only restores the static type.
        return (TResponse)resolved;
    }

    /// <summary>
    /// Returns the correlation id for this request, reusing an inbound one when present so
    /// that a chain of calls shares a single identifier.
    /// </summary>
    /// <param name="httpContext">The current request.</param>
    /// <returns>The correlation id.</returns>
    public static string ResolveCorrelationId(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (httpContext.Request.Headers.TryGetValue(CorrelationIdHeader, out Microsoft.Extensions.Primitives.StringValues inbound) &&
            !string.IsNullOrWhiteSpace(inbound))
        {
            return inbound.ToString();
        }

        return httpContext.TraceIdentifier;
    }
}

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using AdNoir.Shared.Contracts.Messages;
using AdNoir.Shared.Contracts.Responses;

namespace AdNoir.BuildingBlocks.Http;

/// <summary>Wires up the global exception handler.</summary>
public static class ExceptionHandlerExtensions
{
    /// <summary>
    /// Adds the exception handling middleware configured for the response envelope.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The same application builder.</returns>
    /// <remarks>
    /// <para>
    /// A bare <c>UseExceptionHandler()</c> throws at startup unless an error path, a fallback
    /// delegate, or <c>AddProblemDetails()</c> is present. ProblemDetails is ruled out,
    /// and an error path would mean a second pipeline pass, so a no-op
    /// fallback is supplied instead.
    /// </para>
    /// <para>
    /// The fallback genuinely has nothing to do: <see cref="GlobalExceptionHandler"/> writes
    /// the envelope and returns true for every exception it sees. The one case it declines -
    /// a client that disconnected mid-request - is a case where no response can be written
    /// anyway.
    /// </para>
    /// </remarks>
    public static IApplicationBuilder UseAdNoirExceptionHandler(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseExceptionHandler(new ExceptionHandlerOptions
        {
            ExceptionHandler = static _ => Task.CompletedTask,
        });

        // Endpoint selection can reject the content type without throwing a binding exception.
        return app.UseStatusCodePages(context => context.HttpContext.Response.StatusCode switch
        {
            StatusCodes.Status400BadRequest => BaseResponse.Failure(
                ResponseStatus.ValidationFailed, MessageCodes.Common.ValidationFailed)
                .ToHttpResult().ExecuteAsync(context.HttpContext),
            StatusCodes.Status415UnsupportedMediaType => BaseResponse.Failure(
                ResponseStatus.UnsupportedMediaType, MessageCodes.Gateway.UnsupportedMediaType)
                .ToHttpResult().ExecuteAsync(context.HttpContext),
            _ => Task.CompletedTask,
        });
    }
}

using AdNoir.Shared.Contracts.Responses;
using Microsoft.AspNetCore.Http;

namespace AdNoir.BuildingBlocks.Http;

/// <summary>
/// Turns a service response into an HTTP result. This is the only conversion endpoints use.
/// </summary>
public static class ResponseResultExtensions
{
    /// <summary>Writes the response as JSON with the status code its outcome maps to.</summary>
    /// <typeparam name="TResponse">The concrete response type.</typeparam>
    /// <param name="response">The response returned by the service.</param>
    /// <returns>An HTTP result that enriches and serializes the response.</returns>
    public static IResult ToHttpResult<TResponse>(this TResponse response)
        where TResponse : BaseResponse =>
        new BaseResponseResult<TResponse>(response);

    /// <summary>Awaits a pending response and writes it as JSON.</summary>
    /// <typeparam name="TResponse">The concrete response type.</typeparam>
    /// <param name="responseTask">The pending response.</param>
    /// <returns>An HTTP result that enriches and serializes the response.</returns>
    public static async Task<IResult> ToHttpResultAsync<TResponse>(this Task<TResponse> responseTask)
        where TResponse : BaseResponse
    {
        ArgumentNullException.ThrowIfNull(responseTask);

        TResponse response = await responseTask;
        return response.ToHttpResult();
    }
}

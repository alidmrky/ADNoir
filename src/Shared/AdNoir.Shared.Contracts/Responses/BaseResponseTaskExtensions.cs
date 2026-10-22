namespace AdNoir.Shared.Contracts.Responses;

/// <summary>
/// Awaitable counterparts of the functional composition methods. Without these
/// every chained step would need its own <c>await</c> and intermediate variable, which is
/// exactly the ceremony the envelope is supposed to remove.
/// </summary>
public static class BaseResponseTaskExtensions
{
    /// <summary>Transforms the payload of a pending response when it succeeds.</summary>
    /// <typeparam name="T">The source payload type.</typeparam>
    /// <typeparam name="TOut">The transformed payload type.</typeparam>
    /// <param name="responseTask">The pending response.</param>
    /// <param name="mapper">Transformation applied to the payload.</param>
    /// <returns>A response carrying the transformed payload, or the original failure.</returns>
    public static async Task<BaseResponse<TOut>> MapAsync<T, TOut>(
        this Task<BaseResponse<T>> responseTask,
        Func<T, TOut> mapper)
    {
        ArgumentNullException.ThrowIfNull(responseTask);

        BaseResponse<T> response = await responseTask;
        return response.Map(mapper);
    }

    /// <summary>Chains an asynchronous step onto a pending response. The first failure short-circuits.</summary>
    /// <typeparam name="T">The source payload type.</typeparam>
    /// <typeparam name="TOut">The payload type of the next step.</typeparam>
    /// <param name="responseTask">The pending response.</param>
    /// <param name="binder">The next step.</param>
    /// <returns>The next step's response, or the original failure.</returns>
    public static async Task<BaseResponse<TOut>> BindAsync<T, TOut>(
        this Task<BaseResponse<T>> responseTask,
        Func<T, Task<BaseResponse<TOut>>> binder)
    {
        ArgumentNullException.ThrowIfNull(responseTask);
        ArgumentNullException.ThrowIfNull(binder);

        BaseResponse<T> response = await responseTask;

        return response.IsSuccess && response.Data is not null
            ? await binder(response.Data)
            : BaseResponse<TOut>.Propagate(response);
    }

    /// <summary>Turns a pending successful response into a failure when the payload fails a check.</summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="responseTask">The pending response.</param>
    /// <param name="predicate">Check the payload must satisfy.</param>
    /// <param name="messageCode">Catalog key used when the check fails.</param>
    /// <param name="status">Status used when the check fails.</param>
    /// <param name="args">Optional named placeholder values.</param>
    /// <returns>The response, or a failure when the check does not hold.</returns>
    public static async Task<BaseResponse<T>> EnsureAsync<T>(
        this Task<BaseResponse<T>> responseTask,
        Func<T, bool> predicate,
        string messageCode,
        ResponseStatus status = ResponseStatus.BusinessRuleViolation,
        params (string Key, object? Value)[] args)
    {
        ArgumentNullException.ThrowIfNull(responseTask);

        BaseResponse<T> response = await responseTask;
        return response.Ensure(predicate, messageCode, status, args);
    }

    /// <summary>Runs a side effect on the payload of a pending response when it succeeds.</summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="responseTask">The pending response.</param>
    /// <param name="action">Side effect to run.</param>
    /// <returns>The response, unchanged.</returns>
    public static async Task<BaseResponse<T>> TapAsync<T>(
        this Task<BaseResponse<T>> responseTask,
        Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(responseTask);

        BaseResponse<T> response = await responseTask;
        return response.Tap(action);
    }

    /// <summary>Collapses a pending envelope into a single value.</summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="responseTask">The pending response.</param>
    /// <param name="onSuccess">Applied to the payload when successful.</param>
    /// <param name="onFailure">Applied to the response when it is a failure.</param>
    /// <returns>The value produced by whichever branch ran.</returns>
    public static async Task<TResult> MatchAsync<T, TResult>(
        this Task<BaseResponse<T>> responseTask,
        Func<T, TResult> onSuccess,
        Func<BaseResponse<T>, TResult> onFailure)
    {
        ArgumentNullException.ThrowIfNull(responseTask);

        BaseResponse<T> response = await responseTask;
        return response.Match(onSuccess, onFailure);
    }
}

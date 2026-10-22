using AdNoir.Shared.Contracts.Messages;

namespace AdNoir.Shared.Contracts.Responses;

/// <summary>
/// A page of results. Paging information is typed here rather than pushed into
/// <see cref="BaseResponse.Metadata"/>: once typed data starts going into the metadata bag,
/// the contract stops being readable.
/// </summary>
/// <typeparam name="T">The item type. A DTO, never a domain entity.</typeparam>
public record PagedResponse<T> : BaseResponse<IReadOnlyList<T>>
{
    /// <summary>Initializes a new instance. Not public by design — use <see cref="Create"/>.</summary>
    protected PagedResponse()
    {
    }

    /// <summary>The 1-based page number that was returned.</summary>
    public int PageNumber { get; init; }

    /// <summary>The maximum number of items per page.</summary>
    public int PageSize { get; init; }

    /// <summary>Total number of items across all pages.</summary>
    public long TotalCount { get; init; }

    /// <summary>Total number of pages, derived from <see cref="TotalCount"/> and <see cref="PageSize"/>.</summary>
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    /// <summary>Whether a page follows this one.</summary>
    public bool HasNext => PageNumber < TotalPages;

    /// <summary>Whether a page precedes this one.</summary>
    public bool HasPrevious => PageNumber > 1;

    /// <summary>Creates a page of results.</summary>
    /// <param name="items">The items on this page.</param>
    /// <param name="pageNumber">The 1-based page number.</param>
    /// <param name="pageSize">The maximum number of items per page.</param>
    /// <param name="totalCount">Total number of items across all pages.</param>
    /// <param name="messageCode">Catalog key; defaults to <see cref="MessageCodes.Common.Success"/>.</param>
    /// <returns>A successful paged response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="pageNumber"/> or <paramref name="pageSize"/> is not positive, or
    /// <paramref name="totalCount"/> is negative.
    /// </exception>
    public static PagedResponse<T> Create(
        IReadOnlyList<T> items,
        int pageNumber,
        int pageSize,
        long totalCount,
        string messageCode = MessageCodes.Common.Success)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageNumber);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);
        ArgumentOutOfRangeException.ThrowIfNegative(totalCount);

        return new PagedResponse<T>
        {
            Status = ResponseStatus.Success,
            MessageCode = messageCode,
            Data = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    /// <summary>Creates an empty page, used when a query matches nothing.</summary>
    /// <param name="pageNumber">The 1-based page number.</param>
    /// <param name="pageSize">The maximum number of items per page.</param>
    /// <returns>A successful paged response with no items.</returns>
    public static PagedResponse<T> Empty(int pageNumber = 1, int pageSize = 20) =>
        Create([], pageNumber, pageSize, 0);

    /// <inheritdoc />
    public override PagedResponse<T> WithContext(string? correlationId, string? traceId) =>
        this with { CorrelationId = correlationId, TraceId = traceId };
}

using AdNoir.BuildingBlocks.Services;
using AdNoir.Sample.Application.SampleItems.Dto;
using AdNoir.Shared.Contracts.Responses;

namespace AdNoir.Sample.Application.SampleItems;

/// <summary>
/// Application service contract for <see cref="AdNoir.Sample.Domain.SampleItems.SampleItem"/>
/// management.
/// </summary>
/// <remarks>
/// Implementing <see cref="IApplicationService"/> makes the architecture test enforce that
/// every public method returns <c>Task&lt;BaseResponse&gt;</c> or
/// <c>Task&lt;BaseResponse&lt;T&gt;&gt;</c> with a <c>CancellationToken</c> as the last
/// parameter.
/// </remarks>
public interface ISampleItemService : IApplicationService
{
    /// <summary>Creates a new sample item and publishes the created event via the outbox.</summary>
    /// <param name="request">The creation payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created item wrapped in a response envelope.</returns>
    Task<BaseResponse<SampleItemDto>> CreateAsync(
        CreateSampleItemRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Retrieves a single sample item by its identifier.</summary>
    /// <param name="id">The item identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The item or a not-found response.</returns>
    Task<BaseResponse<SampleItemDto>> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}

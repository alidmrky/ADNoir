using AdNoir.BuildingBlocks.Context;
using AdNoir.Domain.Abstractions.Repositories;
using AdNoir.Sample.Application.SampleItems.Dto;
using AdNoir.Sample.Domain.SampleItems;
using AdNoir.Shared.Contracts.Events;
using AdNoir.Shared.Contracts.Messages;
using AdNoir.Shared.Contracts.Responses;

namespace AdNoir.Sample.Application.SampleItems;

/// <summary>Default <see cref="ISampleItemService"/>.</summary>
/// <remarks>
/// No try/catch anywhere: expected outcomes return as failure responses, unexpected faults
/// rise to the single global handler. Nothing here knows the store is MongoDB.
/// <para>
/// The outbox pattern: <see cref="IIntegrationEventPublisher.PublishAsync{TEvent}"/> is called
/// <em>before</em> <c>CommitAsync</c> so that the event record lands inside the same Mongo
/// transaction as the entity insert. Calling it after commit would silently drop
/// atomicity without any error.
/// </para>
/// </remarks>
public sealed class SampleItemService(
    ISampleItemRepository repository,
    IUnitOfWork unitOfWork,
    IIntegrationEventPublisher publisher,
    ITenantContext tenantContext) : ISampleItemService
{
    private readonly ISampleItemRepository _repository = repository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IIntegrationEventPublisher _publisher = publisher;
    private readonly ITenantContext _tenantContext = tenantContext;

    /// <inheritdoc />
    public async Task<BaseResponse<SampleItemDto>> CreateAsync(
        CreateSampleItemRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        SampleItem item = SampleItem.Create(
            _tenantContext.TenantId,
            request.Title,
            request.Description);

        await _unitOfWork.BeginAsync(cancellationToken);

        await _repository.InsertAsync(item, cancellationToken);

        // Publish BEFORE commit: the event record must be inside the same Mongo transaction
        // as the entity. If this is moved after CommitAsync, atomicity is silently lost.
        await _publisher.PublishAsync(
            new SampleItemCreatedEvent
            {
                TenantId = item.TenantId,
                ItemId = item.Id,
                Title = item.Title,
            },
            cancellationToken);

        await _unitOfWork.CommitAsync(cancellationToken);

        return BaseResponse<SampleItemDto>.Created(
            ToDto(item),
            MessageCodes.Sample.ItemCreated);
    }

    /// <inheritdoc />
    public async Task<BaseResponse<SampleItemDto>> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        SampleItem? item = await _repository.FindByIdAsync(id, cancellationToken);

        return item is null
            ? BaseResponse<SampleItemDto>.NotFound(MessageCodes.Sample.ItemNotFound, ("itemId", id))
            : BaseResponse<SampleItemDto>.Success(ToDto(item));
    }

    private static SampleItemDto ToDto(SampleItem item) =>
        new(
            item.Id,
            item.TenantId,
            item.Title,
            item.Description,
            item.CreatedAtUtc,
            item.CreatedBy);
}

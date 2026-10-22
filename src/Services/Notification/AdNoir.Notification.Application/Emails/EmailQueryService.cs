using AdNoir.BuildingBlocks.Context;
using AdNoir.Domain.Abstractions.Repositories;
using AdNoir.Notification.Application.Emails.Abstractions;
using AdNoir.Notification.Application.Emails.Dtos;
using AdNoir.Notification.Domain.Emails;
using AdNoir.Shared.Contracts.Responses;

namespace AdNoir.Notification.Application.Emails;

/// <summary>Application boundary for tenant-wide notification administration.</summary>
public sealed class EmailQueryService(IRepository<EmailDelivery> repository, IEmailQueries queries, ICurrentUser currentUser, TimeProvider timeProvider) : IEmailQueryService
{
    private bool CanReadTenantHistory => currentUser.IsAuthenticated && currentUser.ReadScope == DataScope.Tenant;

    /// <inheritdoc />
    public async Task<BaseResponse<IReadOnlyList<EmailDeliveryDto>>> ListAsync(EmailDeliveryStatus? status, CancellationToken cancellationToken)
    {
        if (!CanReadTenantHistory)
        {
            return BaseResponse<IReadOnlyList<EmailDeliveryDto>>.Forbidden();
        }
        IReadOnlyList<EmailDelivery> deliveries = await repository.ListLimitedAsync(100,
            status.HasValue ? delivery => delivery.Status == status.Value : null,
            delivery => delivery.CreatedAtUtc, descending: true, cancellationToken: cancellationToken);
        DateTime nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        return BaseResponse<IReadOnlyList<EmailDeliveryDto>>.Success(
            deliveries.Select(delivery => EmailDeliveryMapping.ToDto(delivery, nowUtc)).ToArray());
    }

    /// <inheritdoc />
    public async Task<BaseResponse<EmailDeliveryDto>> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!CanReadTenantHistory)
        {
            return BaseResponse<EmailDeliveryDto>.Forbidden();
        }
        EmailDelivery? delivery = await repository.FindByIdAsync(id, cancellationToken);
        return delivery is null ? BaseResponse<EmailDeliveryDto>.NotFound() : BaseResponse<EmailDeliveryDto>.Success(EmailDeliveryMapping.ToDto(delivery, timeProvider.GetUtcNow().UtcDateTime));
    }

    /// <inheritdoc />
    public async Task<BaseResponse<EmailStatisticsDto>> StatisticsAsync(CancellationToken cancellationToken)
    {
        if (!CanReadTenantHistory)
        {
            return BaseResponse<EmailStatisticsDto>.Forbidden();
        }
        DateTime toUtc = timeProvider.GetUtcNow().UtcDateTime;
        return BaseResponse<EmailStatisticsDto>.Success(await queries.StatisticsAsync(toUtc.AddDays(-30), toUtc, cancellationToken));
    }
}

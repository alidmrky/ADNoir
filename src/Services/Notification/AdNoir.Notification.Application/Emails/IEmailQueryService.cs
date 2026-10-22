using AdNoir.BuildingBlocks.Services;
using AdNoir.Notification.Application.Emails.Dtos;
using AdNoir.Notification.Domain.Emails;
using AdNoir.Shared.Contracts.Responses;

namespace AdNoir.Notification.Application.Emails;

/// <summary>Tenant notification history and statistics application boundary.</summary>
public interface IEmailQueryService : IApplicationService
{
    /// <summary>Returns at most the latest 100 masked deliveries.</summary>
    Task<BaseResponse<IReadOnlyList<EmailDeliveryDto>>> ListAsync(EmailDeliveryStatus? status, CancellationToken cancellationToken);
    /// <summary>Returns a masked delivery and its bounded attempt history.</summary>
    Task<BaseResponse<EmailDeliveryDto>> FindAsync(Guid id, CancellationToken cancellationToken);
    /// <summary>Returns the last thirty days of aggregate statistics.</summary>
    Task<BaseResponse<EmailStatisticsDto>> StatisticsAsync(CancellationToken cancellationToken);
}

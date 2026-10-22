using AdNoir.Notification.Application.Emails.Dtos;

namespace AdNoir.Notification.Application.Emails.Abstractions;

/// <summary>Specialized statistics that cannot be expressed through the generic entity repository.</summary>
public interface IEmailQueries
{
    /// <summary>Aggregates a bounded creation-time cohort within the current read scope.</summary>
    Task<EmailStatisticsDto> StatisticsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken);
}

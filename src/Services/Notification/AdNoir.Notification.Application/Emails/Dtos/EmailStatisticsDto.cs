using AdNoir.Notification.Application.Emails.Abstractions;
using AdNoir.Notification.Application.Emails.Dtos;
using AdNoir.Notification.Domain.Emails;
using AdNoir.Shared.Contracts.Responses;

namespace AdNoir.Notification.Application.Emails.Dtos;

/// <summary>Counts within a bounded UTC creation-time cohort; acceptance is not delivery.</summary>
public sealed record EmailStatisticsDto(DateTime FromUtc, DateTime ToUtc, long Total,
    IReadOnlyDictionary<string, long> ByStatus, long Attempts, long RetriedMessages,
    double? AverageQueueWaitMilliseconds, double? AverageAttemptMilliseconds);

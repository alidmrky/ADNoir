using AdNoir.Notification.Application.Emails.Abstractions;
using AdNoir.Notification.Application.Emails.Dtos;
using AdNoir.Notification.Domain.Emails;
using AdNoir.Shared.Contracts.Responses;

namespace AdNoir.Notification.Application.Emails.Dtos;

/// <summary>One safe attempt projection.</summary>
public sealed record EmailAttemptDto(int Number, DateTime StartedAtUtc, DateTime? CompletedAtUtc,
    string Outcome, int? SmtpStatusCode, long? DurationMilliseconds);

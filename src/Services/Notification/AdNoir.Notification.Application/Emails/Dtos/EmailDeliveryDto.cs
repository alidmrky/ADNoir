using AdNoir.Notification.Application.Emails.Abstractions;
using AdNoir.Notification.Application.Emails.Dtos;
using AdNoir.Notification.Domain.Emails;
using AdNoir.Shared.Contracts.Responses;

namespace AdNoir.Notification.Application.Emails.Dtos;

/// <summary>Masked delivery metadata. Body and full recipient are never returned.</summary>
public sealed record EmailDeliveryDto(Guid Id, Guid SourceEventId, string? CorrelationId,
    string Kind, string TemplateVersion, string Culture, string Provider, string? MaskedRecipient,
    EmailDeliveryStatus Status, DateTime CreatedAtUtc, DateTime? CompletedAtUtc,
    DateTime NextAttemptAtUtc, string? OutcomeCode, bool IsRedacted, IReadOnlyList<EmailAttemptDto> Attempts);

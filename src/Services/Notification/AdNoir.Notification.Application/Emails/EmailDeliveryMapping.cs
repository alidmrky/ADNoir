using AdNoir.Notification.Application.Emails.Dtos;
using AdNoir.Notification.Domain.Emails;

namespace AdNoir.Notification.Application.Emails;

/// <summary>Maps delivery history without exposing message content or an unmasked recipient.</summary>
internal static class EmailDeliveryMapping
{
    public static EmailDeliveryDto ToDto(EmailDelivery row, DateTime nowUtc)
    {
        bool redact = row.IsRedacted || row.RedactAtUtc <= nowUtc;
        string? address = redact ? null : row.Recipient;
        string? masked = address is null ? null : "***@" + address[(address.LastIndexOf('@') + 1)..];
        return new(row.Id, row.SourceEventId, redact ? null : row.CorrelationId, row.Kind, row.TemplateVersion,
            row.Culture, row.Provider, masked, row.Status, row.CreatedAtUtc, row.CompletedAtUtc,
            row.NextAttemptAtUtc, row.OutcomeCode, redact,
            row.Attempts.Select(x => new EmailAttemptDto(x.Number, x.StartedAtUtc, x.CompletedAtUtc,
                x.Outcome, x.SmtpStatusCode, x.DurationMilliseconds)).ToArray());
    }
}

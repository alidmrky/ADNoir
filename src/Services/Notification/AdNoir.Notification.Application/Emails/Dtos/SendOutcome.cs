using AdNoir.Notification.Domain.Emails;
namespace AdNoir.Notification.Application.Emails.Dtos;

/// <summary>Provider-neutral dispatch result. Acceptance never asserts delivery.</summary>
public sealed record SendOutcome(EmailDeliveryStatus Status, string Code, int? SmtpStatusCode = null);

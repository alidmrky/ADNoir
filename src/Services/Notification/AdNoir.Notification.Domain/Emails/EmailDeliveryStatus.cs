using AdNoir.Domain.Abstractions.Entities;
namespace AdNoir.Notification.Domain.Emails;

/// <summary>Delivery states distinguish SMTP acceptance from actual inbox delivery.</summary>
public enum EmailDeliveryStatus
{
    /// <summary>Awaiting an attempt.</summary>
    Pending,
    /// <summary>A worker has acquired the delivery.</summary>
    Processing,
    /// <summary>The SMTP server accepted the message; delivery is not proven.</summary>
    Accepted,
    /// <summary>A definitive temporary rejection allows another attempt.</summary>
    RetryScheduled,
    /// <summary>A definitive rejection or exhausted retry budget.</summary>
    Failed,
    /// <summary>Sending is disabled by policy.</summary>
    Skipped,
    /// <summary>Acceptance is unknown. Never automatically resend.</summary>
    OutcomeUnknown,
}

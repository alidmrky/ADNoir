using AdNoir.Domain.Abstractions.Entities;
namespace AdNoir.Notification.Domain.Emails;

/// <summary>One persisted SMTP attempt, recorded before external IO starts.</summary>
public sealed class EmailAttempt
{
    /// <summary>One-based attempt number.</summary>
    public int Number { get; set; }
    /// <summary>Attempt start in UTC.</summary>
    public DateTime StartedAtUtc { get; set; }
    /// <summary>Attempt completion, when known.</summary>
    public DateTime? CompletedAtUtc { get; set; }
    /// <summary>Sanitized machine result, never the SMTP exception message.</summary>
    public string Outcome { get; set; } = "started";
    /// <summary>SMTP status code when available.</summary>
    public int? SmtpStatusCode { get; set; }
    /// <summary>Elapsed milliseconds, measured using a monotonic clock.</summary>
    public long? DurationMilliseconds { get; set; }
}

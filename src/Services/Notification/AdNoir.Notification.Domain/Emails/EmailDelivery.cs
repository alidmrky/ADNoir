using AdNoir.Domain.Abstractions.Entities;
namespace AdNoir.Notification.Domain.Emails;

/// <summary>Durable tenant-owned queue, history and permanent event deduplication tombstone.</summary>
public sealed class EmailDelivery : IEntity, IMultiTenant, IHasVersion
{
    /// <inheritdoc />
    public Guid Id { get; set; } = Guid.CreateVersion7();
    /// <inheritdoc />
    public Guid TenantId { get; set; }
    /// <inheritdoc />
    public long Version { get; set; } = 1;
    /// <summary>Stable source event identity; deduplication survives personal-data redaction.</summary>
    public Guid SourceEventId { get; set; }
    /// <summary>Originating correlation identifier.</summary>
    public string? CorrelationId { get; set; }
    /// <summary>Notification category.</summary>
    public string Kind { get; set; } = "welcome";
    /// <summary>Template revision pinned when queued.</summary>
    public string TemplateVersion { get; set; } = string.Empty;
    /// <summary>Selected supported language.</summary>
    public string Culture { get; set; } = "en";
    /// <summary>Provider identifier; SMTP cannot supply delivery webhooks.</summary>
    public string Provider { get; set; } = "smtp";
    /// <summary>Recipient, removed after the retention deadline.</summary>
    public string? Recipient { get; set; }
    /// <summary>Rendered subject snapshot, removed after retention.</summary>
    public string? Subject { get; set; }
    /// <summary>Rendered plain-text snapshot, removed after retention.</summary>
    public string? Body { get; set; }
    /// <summary>Current state.</summary>
    public EmailDeliveryStatus Status { get; set; }
    /// <summary>Creation time.</summary>
    public DateTime CreatedAtUtc { get; set; }
    /// <summary>Scheduled processing time.</summary>
    public DateTime NextAttemptAtUtc { get; set; }
    /// <summary>Lease expiry, also the recovery deadline for uncertain processing.</summary>
    public DateTime? LeaseUntilUtc { get; set; }
    /// <summary>Opaque worker ownership token.</summary>
    public Guid? LeaseId { get; set; }
    /// <summary>Terminal state time.</summary>
    public DateTime? CompletedAtUtc { get; set; }
    /// <summary>When personal content must be removed.</summary>
    public DateTime? RedactAtUtc { get; set; }
    /// <summary>Whether personal data was removed.</summary>
    public bool IsRedacted { get; set; }
    /// <summary>Sanitized reason for the current outcome.</summary>
    public string? OutcomeCode { get; set; }
    /// <summary>Bounded attempt history.</summary>
    public List<EmailAttempt> Attempts { get; set; } = [];
}

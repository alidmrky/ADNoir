namespace AdNoir.Shared.Contracts.Events;

/// <summary>
/// A fact that has already happened and is published to other services.
/// </summary>
/// <remarks>
/// Named <c>IntegrationEvent</c> rather than anything with "bus" in it: the same codebase
/// will also carry domain events, and the prefix says which kind is meant without a comment.
/// Abbreviated house prefixes such as <c>IADEvent</c> are not used.
/// </remarks>
public interface IIntegrationEvent
{
    /// <summary>
    /// Unique identifier of this event. Doubles as the deduplication key on the consuming
    /// side, because delivery is at-least-once and the same message can arrive twice.
    /// </summary>
    Guid EventId { get; }

    /// <summary>When the fact occurred, in UTC.</summary>
    DateTime OccurredAtUtc { get; }

    /// <summary>
    /// The tenant this event belongs to. Consumers have no HTTP context, so the tenant scope
    /// is rebuilt from this field. Null only for genuinely tenant-agnostic events.
    /// </summary>
    Guid? TenantId { get; }

    /// <summary>Correlation identifier carried across service boundaries.</summary>
    string? CorrelationId { get; }
}

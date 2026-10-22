namespace AdNoir.Shared.Contracts.Events;

/// <summary>
/// Base record for integration events, supplying the identity and context fields every
/// event needs. Deriving from it is a convenience, not a requirement — implementing
/// <see cref="IIntegrationEvent"/> directly is equally valid.
/// </summary>
public abstract record IntegrationEvent : IIntegrationEvent
{
    /// <summary>
    /// Unique identifier of this event, time-ordered so that storage and indexes stay
    /// sequential.
    /// </summary>
    public Guid EventId { get; init; } = Guid.CreateVersion7();

    /// <summary>When the fact occurred, in UTC.</summary>
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>The tenant this event belongs to.</summary>
    public Guid? TenantId { get; init; }

    /// <summary>Correlation identifier carried across service boundaries.</summary>
    public string? CorrelationId { get; init; }
}

namespace AdNoir.Shared.Contracts.Events;

/// <summary>
/// Published when a new sample item is created (pattern demonstration).
/// </summary>
/// <remarks>
/// <para>
/// This event exists to show the outbox and consumer patterns end-to-end. The Sample service
/// itself is intentionally disposable; the event travels with it and will be removed or
/// replaced when the service evolves into something real.
/// </para>
/// </remarks>
public sealed record SampleItemCreatedEvent : IntegrationEvent
{
    /// <summary>The identifier of the created item.</summary>
    public Guid ItemId { get; init; }

    /// <summary>The title of the created item.</summary>
    public string Title { get; init; } = string.Empty;
}

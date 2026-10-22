namespace AdNoir.Shared.Contracts.Events;

/// <summary>
/// Publishes integration events. This is the entire messaging surface the Application layer
/// is allowed to see.
/// </summary>
/// <remarks>
/// <para>
/// The interface is deliberately thin. Wrapping MassTransit wholesale is a known
/// anti-pattern: it hides retry filters, sagas and the test harness — the very features the
/// library was chosen for. So the boundary is drawn here and nowhere else. Application code
/// injects this interface; Infrastructure implements it over
/// <c>IPublishEndpoint</c> and uses MassTransit's own API directly for consumers, retry
/// policies and topology. An architecture test forbids the MassTransit namespace in Domain
/// and Application.
/// </para>
/// <para>
/// Publishing does not hit the broker immediately. The implementation writes to the
/// transactional outbox inside the same MongoDB transaction as the data change, so the two
/// either both persist or neither does. This means <c>PublishAsync</c> must be called
/// <em>before</em> the transaction commits; calling it afterwards silently drops the message
/// outside the transaction and the atomicity guarantee is lost without any error.
/// </para>
/// </remarks>
public interface IIntegrationEventPublisher
{
    /// <summary>Publishes an event to all interested services.</summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="integrationEvent">The event to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes once the event has been enqueued for delivery.</returns>
    Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : class, IIntegrationEvent;
}

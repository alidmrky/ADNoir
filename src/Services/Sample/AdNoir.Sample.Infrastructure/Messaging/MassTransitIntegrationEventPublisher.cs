using AdNoir.Shared.Contracts.Events;
using MassTransit;

namespace AdNoir.Sample.Infrastructure.Messaging;

/// <summary>
/// Implements <see cref="IIntegrationEventPublisher"/> over MassTransit's
/// <see cref="IPublishEndpoint"/> so that Application code never sees MassTransit.
/// </summary>
/// <remarks>
/// <para>
/// Publishing writes to the transactional outbox, not directly to the broker. The outbox
/// record is inside the same Mongo transaction as the caller's data change because the session
/// is shared. This means the caller must call <c>PublishAsync</c> before
/// <c>IUnitOfWork.CommitAsync</c> — calling it after commit drops the event outside the
/// transaction silently.
/// </para>
/// <para>
/// MassTransit's <see cref="IPublishEndpoint"/> is an infrastructure detail: it is injected
/// here rather than into Application or Domain (arch test enforces this).
/// </para>
/// </remarks>
public sealed class MassTransitIntegrationEventPublisher(IPublishEndpoint publishEndpoint)
    : IIntegrationEventPublisher
{
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;

    /// <inheritdoc />
    public Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : class, IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // IPublishEndpoint.Publish routes through the bus outbox when UseBusOutbox is
        // configured for this scope. The outbox delivery service (bus hosted service)
        // then forwards it to RabbitMQ after the transaction commits.
        return _publishEndpoint.Publish(integrationEvent, cancellationToken);
    }
}

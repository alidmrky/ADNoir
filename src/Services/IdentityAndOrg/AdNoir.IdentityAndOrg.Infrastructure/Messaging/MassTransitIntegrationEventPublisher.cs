using AdNoir.Shared.Contracts.Events;
using AdNoir.BuildingBlocks.Http;
using Microsoft.AspNetCore.Http;
using MassTransit;

namespace AdNoir.IdentityAndOrg.Infrastructure.Messaging;

/// <summary>
/// Implements <see cref="IIntegrationEventPublisher"/> over MassTransit's
/// <see cref="IPublishEndpoint"/> (Application never sees MassTransit directly).
/// </summary>
/// <remarks>
/// Publishing writes to the transactional outbox, not directly to the broker. The outbox
/// record is inside the same Mongo transaction as the caller's data change because the session
/// is shared. Callers must invoke <c>PublishAsync</c> before
/// <c>IUnitOfWork.CommitAsync</c> — moving it after commit silently drops atomicity.
/// </remarks>
public sealed class MassTransitIntegrationEventPublisher(IPublishEndpoint publishEndpoint, IHttpContextAccessor httpContextAccessor)
    : IIntegrationEventPublisher
{
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;

    /// <inheritdoc />
    public Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : class, IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return _publishEndpoint.Publish(integrationEvent, context =>
        {
            context.MessageId = integrationEvent.EventId;
            string? correlationId = integrationEvent.CorrelationId;
            if (correlationId is null && httpContextAccessor.HttpContext is HttpContext http)
            {
                correlationId = ResponseEnricher.ResolveCorrelationId(http);
            }
            if (correlationId is { Length: > 0 and <= 128 })
            {
                context.Headers.Set("adnoir-correlation-id", correlationId);
            }
        }, cancellationToken);
    }
}

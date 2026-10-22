using AdNoir.Shared.Contracts.Events;
using AdNoir.Domain.Abstractions.Repositories;
using AdNoir.BuildingBlocks.Mongo.Sessions;
using Microsoft.Extensions.DependencyInjection.Extensions;
using AdNoir.BuildingBlocks.Configuration;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace AdNoir.Sample.Infrastructure.Messaging;

/// <summary>
/// MassTransit bus registration for the Sample service.
/// </summary>
/// <remarks>
/// <para>
/// This class lives in Infrastructure because MassTransit is an infrastructure concern.
/// Application code never references MassTransit directly; it publishes through
/// <see cref="AdNoir.Shared.Contracts.Events.IIntegrationEventPublisher"/>.
/// </para>
/// <para>
/// Queue and exchange names are set explicitly. Relying on MassTransit's
/// type-name defaults means renaming an event type changes the queue name in production.
/// </para>
/// </remarks>
public static class MassTransitConfiguration
{
    /// <summary>
    /// Registers MassTransit with RabbitMQ transport and MongoDB transactional outbox.
    /// When <c>AdNoir:RabbitMq:Host</c> is empty the in-memory transport is used instead
    /// (development without a running broker).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddSampleMassTransit(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        RabbitMqOptions rabbitMq = configuration
            .GetSection(RabbitMqOptions.SectionName)
            .Get<RabbitMqOptions>() ?? new RabbitMqOptions();

        // The outbox is registered for both transports and must share the repository session.
        services.Replace(ServiceDescriptor.Scoped<IMongoSessionAccessor, MassTransitMongoSessionAccessor>());
        services.Replace(ServiceDescriptor.Scoped<IUnitOfWork, MassTransitMongoUnitOfWork>());

        services.AddMassTransit(x =>
        {
            x.AddConsumer<UserRoleUpdatedConsumer>();

            // MongoDB transactional outbox.
            // The outbox collection is in the same database as the application data so that a
            // single Mongo transaction covers both. The delivery service runs in-process.
            x.AddMongoDbOutbox(o =>
            {
                o.ClientFactory(provider => provider.GetRequiredService<IMongoClient>());
                o.DatabaseFactory(provider =>
                {
                    MongoOptions opts = provider.GetRequiredService<IOptions<MongoOptions>>().Value;
                    return provider.GetRequiredService<IMongoClient>().GetDatabase(opts.DatabaseName);
                });

                o.UseBusOutbox();
            });

            if (!string.IsNullOrWhiteSpace(rabbitMq.Host))
            {
                // Full broker transport (RabbitMQ with quorum queues).
                x.UsingRabbitMq((ctx, cfg) =>
                {
                    cfg.Message<UserRoleUpdatedEvent>(m => m.SetEntityName("identity.user-role-updated"));
                    cfg.Message<SampleItemCreatedEvent>(m => m.SetEntityName("sample.item-created"));

                    cfg.Host(rabbitMq.Host, rabbitMq.Port, rabbitMq.VirtualHost, h =>
                    {
                        h.Username(rabbitMq.Username);
                        h.Password(rabbitMq.Password);
                    });

                    // Explicit endpoint name: type-name defaults change if the event is renamed.
                    cfg.ReceiveEndpoint("sample.user-role-updated", e =>
                    {
                        // Quorum queue for durability.
                        e.SetQueueArgument("x-queue-type", "quorum");

                        e.PrefetchCount = 16;
                        ConfigureUserRoleUpdatedEndpoint(ctx, e);
                    });
                });
            }
            else
            {
                // Development/test without a broker: in-memory transport.
                // Loses messages on restart and cannot demonstrate broker behaviour.
                // The outbox rollback check MUST run against the real transport.
                x.UsingInMemory((ctx, cfg) =>
                {
                    cfg.Message<UserRoleUpdatedEvent>(m => m.SetEntityName("identity.user-role-updated"));
                    cfg.Message<SampleItemCreatedEvent>(m => m.SetEntityName("sample.item-created"));
                    cfg.ReceiveEndpoint("sample.user-role-updated", e =>
                        ConfigureUserRoleUpdatedEndpoint(ctx, e));
                });
            }
        });

        return services;
    }

    private static void ConfigureUserRoleUpdatedEndpoint(
        IBusRegistrationContext context,
        IReceiveEndpointConfigurator endpoint)
    {
        endpoint.ConcurrentMessageLimit = 8;
        // Retry wraps the outbox so each attempt gets a fresh transactional scope.
        endpoint.UseMessageRetry(r =>
            r.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(5)));
        endpoint.UseMongoDbOutbox(context);
        endpoint.ConfigureConsumer<UserRoleUpdatedConsumer>(context);
    }
}
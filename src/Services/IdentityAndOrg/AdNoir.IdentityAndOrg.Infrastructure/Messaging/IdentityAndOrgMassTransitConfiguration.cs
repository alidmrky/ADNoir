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

namespace AdNoir.IdentityAndOrg.Infrastructure.Messaging;

/// <summary>
/// MassTransit bus registration for the IdentityAndOrg service.
/// </summary>
/// <remarks>
/// IdentityAndOrg does not consume messages from other services — it is a producer
/// (it publishes <c>UserRoleUpdatedEvent</c>). Consumers are added here when needed.
/// </remarks>
public static class IdentityAndOrgMassTransitConfiguration
{
    /// <summary>
    /// Registers MassTransit with RabbitMQ transport and MongoDB transactional outbox.
    /// In-memory transport is used when RabbitMQ is not configured.
    /// </summary>
    public static IServiceCollection AddIdentityAndOrgMassTransit(
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
            // MongoDB transactional outbox.
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
                x.UsingRabbitMq((ctx, cfg) =>
                {
                    cfg.Message<UserRoleUpdatedEvent>(m => m.SetEntityName("identity.user-role-updated"));
                    cfg.Message<UserRegisteredEvent>(m => m.SetEntityName("identity.user-registered"));

                    cfg.Host(rabbitMq.Host, rabbitMq.Port, rabbitMq.VirtualHost, h =>
                    {
                        h.Username(rabbitMq.Username);
                        h.Password(rabbitMq.Password);
                    });

                    cfg.ConfigureEndpoints(ctx);
                });
            }
            else
            {
                x.UsingInMemory((ctx, cfg) =>
                {
                    cfg.Message<UserRoleUpdatedEvent>(m => m.SetEntityName("identity.user-role-updated"));
                    cfg.Message<UserRegisteredEvent>(m => m.SetEntityName("identity.user-registered"));
                    cfg.ConfigureEndpoints(ctx);
                });
            }
        });

        return services;
    }
}

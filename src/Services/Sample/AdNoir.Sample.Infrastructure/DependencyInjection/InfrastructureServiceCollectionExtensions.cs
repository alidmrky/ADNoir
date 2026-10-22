using AdNoir.BuildingBlocks.Mongo.DependencyInjection;
using AdNoir.BuildingBlocks.Mongo.Indexing;
using AdNoir.Sample.Application.SampleItems;
using AdNoir.Sample.Domain.SampleItems;
using AdNoir.Sample.Infrastructure.Messaging;
using AdNoir.Sample.Infrastructure.Persistence;
using AdNoir.Sample.Infrastructure.Persistence.Indexes;
using AdNoir.Shared.Contracts.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AdNoir.Sample.Infrastructure.DependencyInjection;

/// <summary>Registers the Sample service's infrastructure layer.</summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers MongoDB, MassTransit, the repository and the index provider.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddSampleInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddAdNoirMongo(configuration);

        // Before any collection is touched: a class map registered after a type has already
        // been serialized does not apply to it.
        SampleClassMaps.Register();

        services.AddScoped<ISampleItemRepository, MongoSampleItemRepository>();

        // MassTransit + transactional outbox. This registration replaces the
        // standalone session and unit of work regardless of the selected transport.
        services.AddSampleMassTransit(configuration);

        // Replace (not TryAdd): IdentityAndOrg may have registered the pending publisher if
        // it runs in the same process (it does not — separate service — but defensive is safe).
        // Using Replace ensures the real publisher always wins (AGENTS.md §12).
        services.Replace(
            ServiceDescriptor.Scoped<IIntegrationEventPublisher, MassTransitIntegrationEventPublisher>());

        services.AddSampleIndexProviders();

        return services;
    }

    /// <summary>
    /// Registers the index providers. Called by the API and by <c>AdNoir.DbMigrator</c>.
    /// </summary>
    public static IServiceCollection AddSampleIndexProviders(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IMongoIndexProvider, SampleItemIndexProvider>();

        return services;
    }
}


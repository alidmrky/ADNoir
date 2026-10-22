using AdNoir.BuildingBlocks.Configuration;
using AdNoir.BuildingBlocks.Auditing;
using AdNoir.BuildingBlocks.Mongo.Auditing;
using AdNoir.BuildingBlocks.Mongo.Conventions;
using AdNoir.BuildingBlocks.Mongo.Repositories;
using AdNoir.BuildingBlocks.Mongo.Sessions;
using AdNoir.Domain.Abstractions.Entities;
using AdNoir.Domain.Abstractions.Repositories;
using MassTransit.MongoDbIntegration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace AdNoir.BuildingBlocks.Mongo.DependencyInjection;

/// <summary>MongoDB registration for services that persist data.</summary>
public static class MongoServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Mongo client, database, session accessor, unit of work and audit trail.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddAdNoirMongo(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Must happen before any class map is built; registering later has no effect on
        // types that were already serialized once.
        AdNoirMongoConventions.Register();

        services.Configure<MongoOptions>(configuration.GetSection(MongoOptions.SectionName));
        services.Configure<AuditTrailOptions>(configuration.GetSection(AuditTrailOptions.SectionName));

        services.TryAddSingleton<IMongoClient>(provider =>
        {
            MongoOptions options = provider.GetRequiredService<IOptions<MongoOptions>>().Value;
            return new MongoClient(options.ConnectionString);
        });

        services.TryAddSingleton(provider =>
        {
            MongoOptions options = provider.GetRequiredService<IOptions<MongoOptions>>().Value;
            return provider.GetRequiredService<IMongoClient>().GetDatabase(options.DatabaseName);
        });

        services.TryAddSingleton(TimeProvider.System);

        // Hosts without an outbox use a standalone session. Each service registering a
        // transactional outbox replaces this default, independently of its transport.
#pragma warning disable CS0618 // Hosts without an outbox still require a standalone session.
        services.TryAddScoped<IMongoSessionAccessor, StandaloneMongoSessionAccessor>();
#pragma warning restore CS0618
        services.TryAddScoped<IUnitOfWork, MongoUnitOfWork>();

        services.AddAdNoirAuditTrail();

        return services;
    }

    /// <summary>Registers the audit trail queue, masker and background writer.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddAdNoirAuditTrail(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<ChannelAuditTrailWriter>();
        services.TryAddSingleton<IAuditTrailWriter>(provider =>
            provider.GetRequiredService<ChannelAuditTrailWriter>());
        services.TryAddSingleton<AuditFieldMasker>();

        services.AddHostedService<AuditTrailBackgroundService>();

        return services;
    }

    /// <summary>Opts a host into transactional audit recording using its current-user context.</summary>
    public static IServiceCollection AddAdNoirTransactionalAuditTrail(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<ITransactionalAuditRecorder, TransactionalAuditRecorder>();
        return services;
    }

    /// <summary>
    /// Registers the generic repository for an entity type, exposed both as
    /// <see cref="IRepository{TEntity}"/> and as
    /// <see cref="IUnsafeCrossTenantQuery{TEntity}"/>.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    /// <remarks>
    /// The cross-tenant interface is registered separately and on purpose: injecting it is a
    /// visible act in a constructor, and every method that calls it must carry
    /// <c>[AllowsCrossTenantAccess]</c> with a reason.
    /// </remarks>
    public static IServiceCollection AddMongoRepository<TEntity>(this IServiceCollection services)
        where TEntity : class, IEntity
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<MongoRepository<TEntity>>();
        services.TryAddScoped<IRepository<TEntity>>(provider =>
            provider.GetRequiredService<MongoRepository<TEntity>>());
        services.TryAddScoped<IUnsafeCrossTenantQuery<TEntity>>(provider =>
            provider.GetRequiredService<MongoRepository<TEntity>>());

        return services;
    }
}


using AdNoir.BuildingBlocks.Authorization;
using AdNoir.BuildingBlocks.Mongo.DependencyInjection;
using AdNoir.BuildingBlocks.Mongo.Indexing;
using AdNoir.BuildingBlocks.Mongo.Repositories;
using AdNoir.IdentityAndOrg.Application.Identity.Abstractions;
using AdNoir.IdentityAndOrg.Domain.Identity;
using AdNoir.IdentityAndOrg.Domain.OrgUnits;
using AdNoir.IdentityAndOrg.Domain.Tenants;
using AdNoir.IdentityAndOrg.Infrastructure.Messaging;
using AdNoir.IdentityAndOrg.Infrastructure.Persistence;
using AdNoir.IdentityAndOrg.Infrastructure.Persistence.Indexes;
using AdNoir.IdentityAndOrg.Infrastructure.Security;
using AdNoir.Shared.Contracts.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AdNoir.IdentityAndOrg.Infrastructure.DependencyInjection;

/// <summary>Registers the service's infrastructure layer.</summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>Registers MongoDB, the repositories and the index providers.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddIdentityAndOrgInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddAdNoirMongo(configuration);
        services.AddAdNoirTransactionalAuditTrail();

        // Before any collection is touched: a class map registered after a type has already
        // been serialized does not apply to it.
        IdentityAndOrgClassMaps.Register();

        services.AddScoped<ITenantRepository, MongoTenantRepository>();
        services.AddScoped<IOrgUnitRepository, MongoOrgUnitRepository>();
        services.AddScoped<IOrgUnitSubtreeResolver, OrgUnitSubtreeResolver>();
        services.AddScoped<IUserRepository, MongoUserRepository>();
        services.AddScoped<IRoleRepository, MongoRoleRepository>();
        services.AddScoped<IRefreshTokenRepository, MongoRefreshTokenRepository>();

        services.AddIdentityAndOrgSecurity();
        services.AddScoped<IUserSessionValidator, LocalUserSessionValidator>();
        services.Replace(ServiceDescriptor.Scoped<IPermissionMatrixProvider, LocalPermissionMatrixProvider>());
        services.AddIdentityAndOrgIndexProviders();

        // MassTransit + transactional outbox.
        // This registration replaces the standalone session and unit of work with the
        // shared MassTransit context regardless of the selected transport.
        services.AddIdentityAndOrgMassTransit(configuration);

        // Replace ensures the real publisher wins even if a TryAdd-based default snuck in
        // (AGENTS.md §12).
        services.Replace(
            ServiceDescriptor.Scoped<IIntegrationEventPublisher, MassTransitIntegrationEventPublisher>());

        return services;
    }

    /// <summary>
    /// Registers the index providers. Used by the API for reference and by
    /// <c>AdNoir.DbMigrator</c>, which is what actually creates the indexes.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddIdentityAndOrgIndexProviders(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IMongoIndexProvider, TenantIndexProvider>();
        services.AddSingleton<IMongoIndexProvider, OrgUnitIndexProvider>();
        services.AddSingleton<IMongoIndexProvider, UserIndexProvider>();
        services.AddSingleton<IMongoIndexProvider, RoleIndexProvider>();
        services.AddSingleton<IMongoIndexProvider, RefreshTokenIndexProvider>();
        services.AddSingleton<IMongoIndexProvider, AuditLogIndexProvider>();

        return services;
    }

    /// <summary>Registers password hashing and token signing.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddIdentityAndOrgSecurity(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Singletons: the hasher is stateless and the signing key is loaded once. Creating an
        // RSA key per request would be both slow and, when generated, a different key each time.
        services.TryAddSingleton<IPasswordHasher, IdentityPasswordHasher>();
        services.TryAddSingleton<ISigningKeyProvider, RsaSigningKeyProvider>();
        services.TryAddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();

        return services;
    }
}

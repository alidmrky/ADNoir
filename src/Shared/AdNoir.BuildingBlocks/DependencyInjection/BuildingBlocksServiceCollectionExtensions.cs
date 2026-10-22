using AdNoir.BuildingBlocks.Authorization;
using AdNoir.BuildingBlocks.Configuration;
using AdNoir.BuildingBlocks.Context;
using AdNoir.BuildingBlocks.Http;
using AdNoir.BuildingBlocks.Messages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace AdNoir.BuildingBlocks.DependencyInjection;

/// <summary>
/// Composition root for the shared building blocks. Every service calls this once.
/// </summary>
/// <remarks>
/// The conditional infrastructure branch lives here and nowhere else:
/// service code has no idea whether it is running against Redis or an in-memory cache. That
/// single location is also what makes the Production fail-fast rule enforceable.
/// </remarks>
public static class BuildingBlocksServiceCollectionExtensions
{
    /// <summary>Registers the shared building blocks.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="environment">The host environment.</param>
    /// <returns>The same service collection.</returns>
    /// <exception cref="InvalidOperationException">
    /// Production is missing a required infrastructure connection string.
    /// </exception>
    public static IServiceCollection AddAdNoirBuildingBlocks(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services.AddAdNoirOptions(configuration);
        services.AddAdNoirContext();
        services.AddAdNoirMessages();
        services.AddAdNoirAuthorization();
        services.AddAdNoirCaching(configuration, environment);

        services.AddExceptionHandler<GlobalExceptionHandler>();
        // Route binding failures must reach the shared envelope handler in every environment.
        services.Configure<Microsoft.AspNetCore.Routing.RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);

        return services;
    }

    /// <summary>Binds and validates the configuration sections.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddAdNoirOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<MongoOptions>(configuration.GetSection(MongoOptions.SectionName));
        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<ObservabilityOptions>(configuration.GetSection(ObservabilityOptions.SectionName));
        services.Configure<LocalizationOptions>(configuration.GetSection(LocalizationOptions.SectionName));

        return services;
    }

    /// <summary>Registers the per-request tenant and user context.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddAdNoirContext(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpContextAccessor();
        services.TryAddScoped<ITenantContext, TenantContext>();
        services.TryAddScoped<ICurrentUser, HttpContextCurrentUser>();
        services.TryAddScoped<IClientInfo, HttpContextClientInfo>();

        return services;
    }

    /// <summary>Registers the message catalog and resolver.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddAdNoirMessages(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Singletons: the catalog is read on every response and never changes at runtime.
        services.TryAddSingleton<IMessageCatalog, EmbeddedJsonMessageCatalog>();
        services.TryAddSingleton<IMessageResolver, MessageResolver>();

        return services;
    }

    /// <summary>Registers permission-based authorization.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddAdNoirAuthorization(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddAuthorization();

        // Replace, not TryAdd: AddAuthorization has already registered
        // DefaultAuthorizationPolicyProvider, so a TryAdd here does nothing and every
        // .RequirePermission(...) endpoint fails at runtime with "policy not found" - a
        // failure that only appears when the endpoint is actually called.
        services.Replace(ServiceDescriptor.Singleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>());

        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

        // A deny-everything default so the handler always has its dependency. A service that
        // has a real provider registers it and wins; until one does, authorization fails
        // closed rather than the host failing to start.
        services.TryAddScoped<IPermissionMatrixProvider, UnavailablePermissionMatrixProvider>();

        // Replaces the framework default, which answers 401 and 403 with an empty body.
        // Replace rather than TryAdd for the same reason as the policy provider above: the
        // default is already registered, so a TryAdd would leave those responses bodiless and
        // quietly break the envelope contract for exactly the statuses nobody tests by hand.
        services.Replace(ServiceDescriptor
            .Singleton<IAuthorizationMiddlewareResultHandler, EnvelopeAuthorizationResultHandler>());

        return services;
    }

    /// <summary>
    /// Registers HybridCache, adding the Redis L2 only when one is configured. In Production
    /// a missing connection string is a startup failure, not a downgrade.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="environment">The host environment.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddAdNoirCaching(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        string? redisConnectionString = configuration[$"{RedisOptions.SectionName}:ConnectionString"];

        CacheMode mode = InfrastructureMode.ResolveCache(environment.IsProduction(), redisConnectionString);

        if (mode == CacheMode.Distributed)
        {
            string instanceName = configuration[$"{RedisOptions.SectionName}:InstanceName"] ?? "adnoir";

            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = instanceName;
            });
        }

        services.AddHybridCache();

        return services;
    }
}

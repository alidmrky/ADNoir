using AdNoir.BuildingBlocks.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AdNoir.IdentityAndOrg.Api.Health;

/// <summary>
/// Liveness and readiness probes.
/// </summary>
/// <remarks>
/// The split matters to an orchestrator. Liveness answers "is this process wedged, restart
/// it"; readiness answers "can it serve traffic right now". Reporting a lost database
/// connection as a liveness failure would make the platform restart a perfectly healthy
/// process in a loop while the database is down.
/// </remarks>
public static class HealthCheckExtensions
{
    private const string ReadyTag = "ready";

    /// <summary>Registers the health checks.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddIdentityAndOrgHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        string mongoConnectionString =
            configuration[$"{MongoOptions.SectionName}:ConnectionString"] ?? string.Empty;

        IHealthChecksBuilder builder = services.AddHealthChecks();

        if (!string.IsNullOrWhiteSpace(mongoConnectionString))
        {
            builder.AddMongoDb(
                _ => new MongoDB.Driver.MongoClient(mongoConnectionString),
                name: "mongodb",
                failureStatus: HealthStatus.Unhealthy,
                tags: [ReadyTag]);
        }

        string? redisConnectionString =
            configuration[$"{RedisOptions.SectionName}:ConnectionString"];

        // Only probed when it is actually part of the deployment. Outside Production an
        // unconfigured Redis means the in-memory cache, not a fault.
        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            builder.AddRedis(
                redisConnectionString,
                name: "redis",
                failureStatus: HealthStatus.Degraded,
                tags: [ReadyTag]);
        }

        return services;
    }

    /// <summary>Maps <c>/health/live</c> and <c>/health/ready</c>.</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapAdNoirHealthChecks(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Liveness deliberately runs no checks: reaching this endpoint at all proves the
        // process is up and the pipeline is serving.
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
        });

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(ReadyTag),
        });

        return app;
    }
}

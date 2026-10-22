using AdNoir.BuildingBlocks.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;

namespace AdNoir.Sample.Api.Health;

/// <summary>
/// Liveness and readiness probes for the Sample service.
/// </summary>
public static class SampleHealthCheckExtensions
{
    private const string ReadyTag = "ready";

    /// <summary>Registers the health checks.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddSampleHealthChecks(
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

        string? rabbitMqHost = configuration[$"{RabbitMqOptions.SectionName}:Host"];

        if (!string.IsNullOrWhiteSpace(rabbitMqHost))
        {
            string username = configuration[$"{RabbitMqOptions.SectionName}:Username"] ?? "guest";
            string password = configuration[$"{RabbitMqOptions.SectionName}:Password"] ?? "guest";
            string virtualHost = configuration[$"{RabbitMqOptions.SectionName}:VirtualHost"] ?? "/";

            builder.AddRabbitMQ(
                sp =>
                {
                    ConnectionFactory factory = new()
                    {
                        HostName = rabbitMqHost,
                        UserName = username,
                        Password = password,
                        VirtualHost = virtualHost,
                    };
                    return factory.CreateConnectionAsync();
                },
                name: "rabbitmq",
                failureStatus: HealthStatus.Unhealthy,
                tags: [ReadyTag]);
        }

        return services;
    }

    /// <summary>Maps <c>/health/live</c> and <c>/health/ready</c>.</summary>
    public static IEndpointRouteBuilder MapSampleHealthChecks(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

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

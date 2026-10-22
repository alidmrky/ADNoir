using AdNoir.BuildingBlocks.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace AdNoir.BuildingBlocks.Observability;

/// <summary>
/// OpenTelemetry setup. W3C <c>traceparent</c> propagation is the default in.
/// NET, so a trace started at the gateway continues through HTTP and gRPC calls without any
/// extra wiring.
/// </summary>
public static class OpenTelemetryExtensions
{
    /// <summary>Registers tracing and metrics.</summary>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The same builder.</returns>
    /// <remarks>
    /// The OTLP exporter is registered only when an endpoint is configured. Registering it
    /// against nothing produces a steady trickle of connection errors in the log, which is
    /// how a development machine ends up training everyone to ignore exporter errors.
    /// </remarks>
    public static IHostApplicationBuilder AddAdNoirOpenTelemetry(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        string serviceName =
            builder.Configuration[$"{ObservabilityOptions.SectionName}:ServiceName"] ?? "adnoir";

        string? otlpEndpoint =
            builder.Configuration[$"{ObservabilityOptions.SectionName}:OtlpEndpoint"];

        bool exportEnabled = !string.IsNullOrWhiteSpace(otlpEndpoint);

        builder.Services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(
                serviceName: serviceName,
                serviceVersion: typeof(OpenTelemetryExtensions).Assembly.GetName().Version?.ToString(),
                serviceInstanceId: Environment.MachineName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(options =>
                        options.Filter = context => !IsHealthCheck(context.Request.Path.Value))
                    .AddHttpClientInstrumentation();

                if (exportEnabled)
                {
                    tracing.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint!));
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();

                if (exportEnabled)
                {
                    metrics.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint!));
                }
            });

        return builder;
    }

    /// <summary>
    /// Health probes run every few seconds and say nothing useful about application
    /// behaviour; tracing them buries real requests in noise.
    /// </summary>
    private static bool IsHealthCheck(string? path) =>
        path is not null && path.StartsWith("/health", StringComparison.OrdinalIgnoreCase);
}

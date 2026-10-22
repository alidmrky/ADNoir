using AdNoir.BuildingBlocks.Configuration;
using AdNoir.BuildingBlocks.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace AdNoir.BuildingBlocks.Observability;

/// <summary>
/// Structured logging setup: Serilog writing compact JSON, enriched with
/// trace and correlation identifiers so a log line can be tied back to a request.
/// </summary>
public static class SerilogExtensions
{
    /// <summary>Registers Serilog as the logging provider.</summary>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The same builder.</returns>
    public static IHostApplicationBuilder AddAdNoirSerilog(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        string serviceName =
            builder.Configuration[$"{ObservabilityOptions.SectionName}:ServiceName"] ?? "adnoir";

        string minimumLevelSetting =
            builder.Configuration["AdNoir:Serilog:MinimumLevel"] ?? nameof(LogEventLevel.Information);

        LogEventLevel minimumLevel = Enum.TryParse(minimumLevelSetting, ignoreCase: true, out LogEventLevel parsed)
            ? parsed
            : LogEventLevel.Information;

        builder.Services.AddSerilog(configuration => configuration
            .MinimumLevel.Is(minimumLevel)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
            // Serilog 3+ populates TraceId and SpanId from Activity.Current by itself, so no
            // span enricher package is needed to correlate logs with OpenTelemetry traces.
            .Enrich.FromLogContext()
            .Enrich.WithProperty("service", serviceName)
            .Enrich.WithProperty("environment", builder.Environment.EnvironmentName)
            .WriteTo.Console(new CompactJsonFormatter()));

        return builder;
    }

    /// <summary>
    /// Adds request logging with the correlation id attached, so one line per request carries
    /// the identifier that ties it to downstream calls and to the response envelope.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The same application builder.</returns>
    public static IApplicationBuilder UseAdNoirRequestLogging(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseSerilogRequestLogging(options =>
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("CorrelationId", ResponseEnricher.ResolveCorrelationId(httpContext));
                diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
            });
    }
}

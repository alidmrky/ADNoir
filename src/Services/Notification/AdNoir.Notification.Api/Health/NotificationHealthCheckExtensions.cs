using AdNoir.BuildingBlocks.Http;
using AdNoir.Shared.Contracts.Messages;
using AdNoir.Shared.Contracts.Responses;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AdNoir.Notification.Api.Health;

/// <summary>Health endpoints expose only envelope results, never infrastructure diagnostics.</summary>
public static class NotificationHealthCheckExtensions
{
    /// <summary>Maps liveness and Mongo/bus readiness.</summary>
    public static IEndpointRouteBuilder MapNotificationHealthChecks(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health/live", () => BaseResponse.Success().ToHttpResult()).AllowAnonymous();
        app.MapGet("/health/ready", async (HealthCheckService health, CancellationToken cancellationToken) =>
        {
            HealthReport report = await health.CheckHealthAsync(cancellationToken);
            return (report.Status == HealthStatus.Healthy
                ? BaseResponse.Success()
                : BaseResponse.Failure(ResponseStatus.ServiceUnavailable, MessageCodes.Common.ExternalServiceError)).ToHttpResult();
        }).AllowAnonymous();
        return app;
    }
}

using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using AdNoir.BuildingBlocks.Context;
using AdNoir.BuildingBlocks.Http;
using AdNoir.Gateway.Configuration;
using AdNoir.Shared.Contracts.Messages;
using AdNoir.Shared.Contracts.Responses;
using Microsoft.AspNetCore.RateLimiting;
using RedisRateLimiting;
using StackExchange.Redis;

namespace AdNoir.Gateway.RateLimiting;

/// <summary>Partitions sliding windows by trusted identity or the actual network peer.</summary>
public sealed class GatewayRateLimitPolicy(GatewayOptions options, IConnectionMultiplexer? redis)
    : IRateLimiterPolicy<string>
{
    /// <inheritdoc />
    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => RejectAsync;

    /// <inheritdoc />
    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        if (httpContext.Request.Path.Equals("/health/live", StringComparison.OrdinalIgnoreCase) ||
            httpContext.Request.Path.Equals("/health/ready", StringComparison.OrdinalIgnoreCase))
        {
            return RateLimitPartition.GetNoLimiter("health");
        }

        // Never trust X-Forwarded-For or client-supplied tenant headers for quota identity.
        string peer = httpContext.Connection.RemoteIpAddress?.MapToIPv6().ToString() ?? "unknown";
        string key = $"ip:{peer}";
        int permits = options.AnonymousPermitLimit;
        string path = (httpContext.Request.Path.Value ?? string.Empty).TrimEnd('/');
        bool authenticationEndpoint = path.Equals("/api/v1/auth/login", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/api/v1/auth/register", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/api/v1/auth/refresh", StringComparison.OrdinalIgnoreCase);

        if (authenticationEndpoint)
        {
            key = $"auth:{peer}";
            permits = options.AuthenticationPermitLimit;
        }
        else if (httpContext.User.Identity?.IsAuthenticated == true &&
            Guid.TryParse(httpContext.User.FindFirstValue(AdNoirClaimTypes.TenantId), out Guid tenantId) &&
            Guid.TryParse(httpContext.User.FindFirstValue(AdNoirClaimTypes.Subject), out Guid userId))
        {
            key = $"user:{tenantId:N}:{userId:N}";
            permits = options.AuthenticatedPermitLimit;
        }

        key = $"{options.RateLimitPrefix}:{key}";
        TimeSpan window = TimeSpan.FromSeconds(options.WindowSeconds);
        if (redis is not null)
        {
            return RedisRateLimitPartition.GetSlidingWindowRateLimiter(key, _ => new RedisSlidingWindowRateLimiterOptions
            {
                ConnectionMultiplexerFactory = () => redis,
                PermitLimit = permits,
                Window = window,
            });
        }

        return RateLimitPartition.GetSlidingWindowLimiter(key, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = permits,
            Window = window,
            SegmentsPerWindow = 6,
            QueueLimit = 0,
            AutoReplenishment = true,
        });
    }

    private async ValueTask RejectAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        else
        {
            context.HttpContext.Response.Headers.RetryAfter = options.WindowSeconds.ToString(CultureInfo.InvariantCulture);
        }

        await BaseResponse.Failure(ResponseStatus.RateLimited, MessageCodes.Common.RateLimited)
            .ToHttpResult().ExecuteAsync(context.HttpContext);
    }
}

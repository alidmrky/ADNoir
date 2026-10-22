using System.Threading.RateLimiting;
using AdNoir.BuildingBlocks.Configuration;
using AdNoir.BuildingBlocks.DependencyInjection;
using AdNoir.BuildingBlocks.Http;
using AdNoir.BuildingBlocks.Messages;
using AdNoir.BuildingBlocks.Observability;
using AdNoir.Gateway.Configuration;
using AdNoir.Gateway.Http;
using AdNoir.Gateway.RateLimiting;
using AdNoir.Gateway.Security;
using AdNoir.Shared.Contracts.Messages;
using AdNoir.Shared.Contracts.Responses;
using Microsoft.AspNetCore.RateLimiting;
using StackExchange.Redis;
using Yarp.ReverseProxy.Model;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddDotEnvFileForDevelopment(builder.Environment);
}
builder.AddAdNoirSerilog();
builder.AddAdNoirOpenTelemetry();
builder.Services.AddExceptionHandler<GatewayInfrastructureExceptionHandler>();
builder.Services.AddAdNoirBuildingBlocks(builder.Configuration, builder.Environment);
builder.Services.AddGatewayAuthentication(builder.Configuration, builder.Environment);

GatewayOptions gateway = builder.Configuration.GetSection(GatewayOptions.SectionName).Get<GatewayOptions>() ?? new();
ArgumentOutOfRangeException.ThrowIfNegativeOrZero(gateway.WindowSeconds);
ArgumentOutOfRangeException.ThrowIfNegativeOrZero(gateway.AuthenticationPermitLimit);
ArgumentOutOfRangeException.ThrowIfNegativeOrZero(gateway.AnonymousPermitLimit);
ArgumentOutOfRangeException.ThrowIfNegativeOrZero(gateway.AuthenticatedPermitLimit);
ArgumentException.ThrowIfNullOrWhiteSpace(gateway.RateLimitPrefix);
foreach (string origin in gateway.AllowedOrigins)
{
    if (!Uri.TryCreate(origin, UriKind.Absolute, out Uri? uri) ||
        (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
        origin != uri.GetLeftPart(UriPartial.Authority))
    {
        throw new InvalidOperationException("Gateway CORS origins must be explicit HTTP(S) origins without paths or trailing slashes.");
    }
}

string? redisConnection = builder.Configuration[$"{RedisOptions.SectionName}:ConnectionString"];
CacheMode cacheMode = InfrastructureMode.ResolveCache(builder.Environment.IsProduction(), redisConnection);
if (cacheMode == CacheMode.Distributed)
{
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    {
        ConfigurationOptions options = ConfigurationOptions.Parse(redisConnection!);
        options.AbortOnConnectFail = false;
        options.ConnectTimeout = 3000;
        options.AsyncTimeout = 3000;
        return ConnectionMultiplexer.Connect(options);
    });
}
builder.Services.AddSingleton(gateway);
builder.Services.AddSingleton(provider => new GatewayRateLimitPolicy(
    gateway, provider.GetService<IConnectionMultiplexer>()));
builder.Services.AddRateLimiter();
builder.Services.AddOptions<RateLimiterOptions>().Configure<GatewayRateLimitPolicy>((options, policy) =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(policy.GetPartition);
    options.OnRejected = policy.OnRejected;
});
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    if (gateway.AllowedOrigins.Length > 0)
    {
        policy.WithOrigins(gateway.AllowedOrigins)
            .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
            .WithHeaders("Authorization", "Content-Type", "Accept-Language", "X-Correlation-Id")
            .WithExposedHeaders("Retry-After", "X-Correlation-Id");
    }
}));
builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

WebApplication app = builder.Build();
app.UseAdNoirExceptionHandler();
app.UseAdNoirRequestLogging();
app.UseAdNoirLocalization();
app.UseStatusCodePages(context => GatewayErrorResponses.WriteAsync(context.HttpContext));
app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.Use(async (context, next) =>
{
    // These routes accept JSON only. The service remains responsible for validating the body.
    if (context.GetEndpoint()?.Metadata.GetMetadata<RouteModel>() is not null &&
        (HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsPut(context.Request.Method) ||
         HttpMethods.IsPatch(context.Request.Method)) &&
        (context.Request.ContentLength > 0 || context.Request.Headers.ContainsKey("Transfer-Encoding")) &&
        !context.Request.HasJsonContentType())
    {
        context.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
        return;
    }
    await next(context);
});

app.MapGet("/health/live", () => BaseResponse.Success().ToHttpResult()).AllowAnonymous();
app.MapGet("/health/ready", async (HttpContext context) =>
{
    IConnectionMultiplexer? redis = context.RequestServices.GetService<IConnectionMultiplexer>();
    if (redis is not null)
    {
        try
        {
            await redis.GetDatabase().PingAsync().WaitAsync(context.RequestAborted);
        }
        catch (RedisException)
        {
            return BaseResponse.Failure(ResponseStatus.ServiceUnavailable, MessageCodes.Common.ExternalServiceError).ToHttpResult();
        }
    }
    return BaseResponse.Success().ToHttpResult();
}).AllowAnonymous();
app.MapReverseProxy();

await app.RunAsync();

/// <summary>Gateway entry point.</summary>
public partial class Program;

using AdNoir.BuildingBlocks.Authorization;
using AdNoir.IdentityAndOrg.Grpc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace AdNoir.Sample.Infrastructure.Identity;

/// <summary>Registers Identity's gRPC client and the permission cache decorator.</summary>
public static class IdentityClientRegistration
{
    /// <summary>Registers bounded HTTP resilience; native gRPC retry is disabled.</summary>
    public static IServiceCollection AddSampleIdentityClient(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);
        string address = configuration["AdNoir:Identity:GrpcAddress"] ?? "http://localhost:5082";
        if (!Uri.TryCreate(address, UriKind.Absolute, out Uri? endpoint) ||
            (endpoint.Scheme != Uri.UriSchemeHttps && !(environment.IsDevelopment() && endpoint.IsLoopback && endpoint.Scheme == Uri.UriSchemeHttp)))
        {
            throw new InvalidOperationException("AdNoir:Identity:GrpcAddress must use HTTPS; Development also permits loopback HTTP.");
        }

        ServiceCredential credential = ServiceCredential.Load(configuration, "sample");
        services.AddGrpcClient<UserService.UserServiceClient>(options => options.Address = endpoint)
            .ConfigureHttpClient(client =>
            {
                client.DefaultRequestHeaders.Add(ServiceCredential.IdHeader, credential.ServiceId);
                client.DefaultRequestHeaders.Add(ServiceCredential.KeyHeader, credential.Key);
            })
            .ConfigureChannel(options =>
            {
                options.ServiceConfig = new Grpc.Net.Client.Configuration.ServiceConfig();
                options.DisableResolverServiceConfig = true;
            })
            .AddStandardResilienceHandler(options =>
            {
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(10);
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(3);
                options.Retry.MaxRetryAttempts = 2;
                options.Retry.Delay = TimeSpan.FromMilliseconds(200);
                options.Retry.UseJitter = true;
            });

        services.AddScoped<IUserSessionValidator>(provider =>
            new DelegatingUserSessionValidator(async (tenant, user, stamp, version, cancellationToken) =>
            {
                try
                {
                    UserService.UserServiceClient client = provider.GetRequiredService<UserService.UserServiceClient>();
                    using var call = client.ValidateUserSessionAsync(new ValidateUserSessionRequest
                    {
                        TenantId = tenant.ToString(),
                        UserId = user.ToString(),
                        SecurityStamp = stamp,
                        PermissionVersion = version,
                    }, deadline: DateTime.UtcNow.AddSeconds(5), cancellationToken: cancellationToken);
                    var response = await call.ResponseAsync;
                    return Enum.IsDefined((UserSessionState)response.State) ? (UserSessionState)response.State : UserSessionState.Invalid;
                }
                catch (Grpc.Core.RpcException exception) when (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException("Session validation cancelled.", exception, cancellationToken);
                }
                catch (Grpc.Core.RpcException exception)
                {
                    throw new AdNoir.Domain.Abstractions.Exceptions.ExternalServiceException(
                        AdNoir.Shared.Contracts.Messages.MessageCodes.Auth.PermissionMatrixUnavailable,
                        "identity", "Live session validation failed.", exception);
                }
            }));
        services.Replace(ServiceDescriptor.Scoped<IPermissionMatrixProvider, GrpcPermissionMatrixProvider>());
        services.Decorate<IPermissionMatrixProvider, CachedPermissionMatrixProvider>();
        return services;
    }
}

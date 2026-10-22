using AdNoir.BuildingBlocks.Configuration;
using AdNoir.BuildingBlocks.Context;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace AdNoir.Gateway.Security;

/// <summary>Verifies JWT claims for quota selection without replacing downstream authorization.</summary>
public static class GatewayAuthentication
{
    /// <summary>Registers direct JWKS retrieval and RS256 bearer validation.</summary>
    public static IServiceCollection AddGatewayAuthentication(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);
        JwtOptions jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new();
        if (string.IsNullOrWhiteSpace(jwt.Issuer) || string.IsNullOrWhiteSpace(jwt.Audience) ||
            !Uri.TryCreate(jwt.JwksUri, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttps &&
                !(environment.IsDevelopment() && uri.IsLoopback && uri.Scheme == Uri.UriSchemeHttp)))
        {
            throw new InvalidOperationException("Gateway requires issuer, audience and HTTPS JWKS; Development permits loopback HTTP.");
        }

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddHttpClient("gateway-jwks", client => client.Timeout = TimeSpan.FromSeconds(10))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            });
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IHttpClientFactory>((options, clients) =>
            {
                options.MapInboundClaims = false;
                options.RequireHttpsMetadata = !environment.IsDevelopment();
                options.ConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                    jwt.JwksUri!, new JwksConfigurationRetriever(jwt.Issuer),
                    new HttpDocumentRetriever(clients.CreateClient("gateway-jwks"))
                    {
                        RequireHttps = !environment.IsDevelopment(),
                    });
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = AdNoirClaimTypes.Subject,
                    RoleClaimType = AdNoirClaimTypes.Roles,
                };
            });
        return services;
    }
}

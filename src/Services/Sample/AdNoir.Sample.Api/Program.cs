using AdNoir.Sample.Infrastructure.Identity;
using AdNoir.Sample.Api.Security;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using AdNoir.BuildingBlocks.Configuration;
using AdNoir.BuildingBlocks.DependencyInjection;
using AdNoir.BuildingBlocks.Http;
using AdNoir.BuildingBlocks.Messages;
using AdNoir.BuildingBlocks.Observability;
using AdNoir.Sample.Api.Endpoints;
using AdNoir.Sample.Api.Health;
using AdNoir.Sample.Application.DependencyInjection;
using AdNoir.Sample.Infrastructure.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Reads .env outside Production. No container runtime on this machine.
builder.Configuration.AddDotEnvFileForDevelopment(builder.Environment);

builder.AddAdNoirSerilog();
builder.AddAdNoirOpenTelemetry();

builder.Services.AddAdNoirBuildingBlocks(builder.Configuration, builder.Environment);
builder.Services.AddSampleInfrastructure(builder.Configuration);
builder.Services.AddSampleApplication();
builder.Services.AddSampleIdentityClient(builder.Configuration, builder.Environment);

// JWT: this service is a consumer of tokens, not an issuer. It fetches the public key from
// Identity's JWKS endpoint. JWKS URI is configured via AdNoir:Jwt:JwksUri in .env.
JwtOptions jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.Issuer) || string.IsNullOrWhiteSpace(jwt.Audience) ||
    !Uri.TryCreate(jwt.JwksUri, UriKind.Absolute, out Uri? jwksUri) ||
    (jwksUri.Scheme != Uri.UriSchemeHttps &&
        !(builder.Environment.IsDevelopment() && jwksUri.IsLoopback && jwksUri.Scheme == Uri.UriSchemeHttp)))
{
    throw new InvalidOperationException("Sample requires JWT issuer, audience and an HTTPS JWKS URI; Development also permits loopback HTTP.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddHttpClient("identity-jwks", client => client.Timeout = TimeSpan.FromSeconds(10))
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
    });

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IHttpClientFactory>((options, httpClientFactory) =>
    {
        options.MapInboundClaims = false;

        if (!string.IsNullOrWhiteSpace(jwt.JwksUri))
        {
            // Fetch public key from Identity's JWKS endpoint.
            // JWKS is a key document, not OIDC discovery metadata.
            options.ConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                jwt.JwksUri,
                new JwksConfigurationRetriever(jwt.Issuer),
                new HttpDocumentRetriever(httpClientFactory.CreateClient("identity-jwks")) { RequireHttps = !builder.Environment.IsDevelopment() });
            options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        }

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
            NameClaimType = AdNoir.BuildingBlocks.Context.AdNoirClaimTypes.Subject,
            RoleClaimType = AdNoir.BuildingBlocks.Context.AdNoirClaimTypes.Roles,
        };
    });

builder.Services.AddOpenApi();
builder.Services.AddSampleHealthChecks(builder.Configuration);

WebApplication app = builder.Build();

// Converts anything unhandled into the response envelope.
app.UseAdNoirExceptionHandler();

app.UseAdNoirRequestLogging();

// Accept-Language → message catalog culture resolution.
app.UseAdNoirLocalization();

app.UseAuthentication();

// After authentication, before authorization.
app.UseAdNoirTenantResolution();
app.UseMiddleware<UserSessionMiddleware>();

app.UseAuthorization();

app.MapSampleItemEndpoints();
app.MapSampleHealthChecks();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options.WithTitle("AdNoir - Sample Service"));
}

await app.RunAsync();

/// <summary>Entry point marker, so the host can be referenced from tests later.</summary>
public partial class Program;

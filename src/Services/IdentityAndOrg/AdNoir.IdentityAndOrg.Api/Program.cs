using AdNoir.BuildingBlocks.Configuration;
using AdNoir.BuildingBlocks.DependencyInjection;
using AdNoir.BuildingBlocks.Http;
using AdNoir.BuildingBlocks.Messages;
using AdNoir.BuildingBlocks.Observability;
using AdNoir.IdentityAndOrg.Api.Endpoints;
using AdNoir.IdentityAndOrg.Api.Grpc;
using AdNoir.IdentityAndOrg.Api.Health;
using AdNoir.IdentityAndOrg.Application.DependencyInjection;
using AdNoir.IdentityAndOrg.Infrastructure.DependencyInjection;
using AdNoir.IdentityAndOrg.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Reads .env outside Production. With no container runtime on this machine there is no
// docker-compose to inject these values, so this is the only path that supplies them (K-4).
builder.Configuration.AddDotEnvFileForDevelopment(builder.Environment);

builder.AddAdNoirSerilog();
builder.AddAdNoirOpenTelemetry();

builder.Services.AddAdNoirBuildingBlocks(builder.Configuration, builder.Environment);
builder.Services.AddIdentityAndOrgInfrastructure(builder.Configuration);
builder.Services.AddIdentityAndOrgApplication();

JwtOptions jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

// Configured through the options pipeline rather than inline, so the signing key can be
// resolved from DI. This service both issues and validates its own tokens, so it uses the key
// directly instead of fetching its own JWKS over HTTP.
builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<ISigningKeyProvider>((options, signingKeyProvider) =>
    {
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = !string.IsNullOrWhiteSpace(jwt.Issuer),
            ValidIssuer = jwt.Issuer,
            ValidateAudience = !string.IsNullOrWhiteSpace(jwt.Audience),
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKeyProvider.SigningKey,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = AdNoir.BuildingBlocks.Context.AdNoirClaimTypes.Subject,
            RoleClaimType = AdNoir.BuildingBlocks.Context.AdNoirClaimTypes.Roles,
        };
    });

builder.Services.AddOpenApi();
builder.Services.AddOutputCache();
builder.Services.AddGrpc();
builder.Services.AddSingleton(new PermissionMatrixCallerGuard(builder.Configuration, builder.Environment));
builder.Services.AddIdentityAndOrgHealthChecks(builder.Configuration);

if (builder.Configuration.GetValue<bool>("AdNoir:RegistrationNotifications:PublishWelcomeEvent") &&
    string.IsNullOrWhiteSpace(builder.Configuration["AdNoir:RabbitMq:Host"]))
{
    throw new InvalidOperationException("Registration notifications require RabbitMQ for cross-process delivery.");
}
builder.Services.Configure<AdNoir.IdentityAndOrg.Application.Identity.RegistrationNotificationOptions>(
    builder.Configuration.GetSection("AdNoir:RegistrationNotifications"));
WebApplication app = builder.Build();

// Converts anything unhandled into the response envelope. First in the pipeline so it also
// covers the middleware below it.
app.UseAdNoirExceptionHandler();

app.UseAdNoirRequestLogging();

// Before anything that can write a response: the message catalog resolves against the
// request's culture, and a 401 from the authorization middleware is already a response.
app.UseAdNoirLocalization();

app.UseAuthentication();

// After authentication, because it reads the token's claims; before authorization, because
// the permission matrix is loaded per tenant.
app.UseAdNoirTenantResolution();
app.UseMiddleware<UserSessionMiddleware>();

app.UseAuthorization();
app.UseOutputCache();

app.MapAuthEndpoints();
app.MapJwksEndpoint();
app.MapTenantEndpoints();
app.MapOrgUnitEndpoints();
app.MapUserEndpoints();
app.MapRoleEndpoints();
app.MapAdNoirHealthChecks();

// Dedicated service credentials and tenant allowlists are checked before repository access.
app.MapGrpcService<PermissionMatrixGrpcService>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options.WithTitle("AdNoir - Identity and Organisation"));
}

await app.RunAsync();

/// <summary>Entry point marker, so the host can be referenced from tests later.</summary>
public partial class Program;

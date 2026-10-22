using AdNoir.BuildingBlocks.Configuration;
using AdNoir.BuildingBlocks.DependencyInjection;
using AdNoir.BuildingBlocks.Http;
using AdNoir.BuildingBlocks.Messages;
using AdNoir.BuildingBlocks.Observability;
using AdNoir.Notification.Api.Endpoints;
using AdNoir.Notification.Api.Health;
using AdNoir.Notification.Api.Http;
using AdNoir.Notification.Api.Security;
using AdNoir.Notification.Application.DependencyInjection;
using AdNoir.Notification.Infrastructure.DependencyInjection;
using AdNoir.Notification.Infrastructure.Identity;
using Scalar.AspNetCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddDotEnvFileForDevelopment(builder.Environment);
}
builder.AddAdNoirSerilog();
builder.AddAdNoirOpenTelemetry();
builder.Services.AddAdNoirBuildingBlocks(builder.Configuration, builder.Environment);
builder.Services.AddNotificationAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddNotificationApplication();
builder.Services.AddNotificationInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddNotificationIdentityClient(builder.Configuration, builder.Environment);
builder.Services.AddOpenApi();

WebApplication app = builder.Build();
app.UseAdNoirExceptionHandler();
app.UseAdNoirRequestLogging();
app.UseAdNoirLocalization();
app.UseStatusCodePages(context => NotificationErrorResponses.WriteAsync(context.HttpContext));
app.UseAuthentication();
app.UseAdNoirTenantResolution();
app.UseMiddleware<UserSessionMiddleware>();
app.UseAuthorization();
app.MapEmailEndpoints();
app.MapNotificationHealthChecks();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options.WithTitle("AdNoir - Notification"));
}
await app.RunAsync();

/// <summary>Notification host entry point.</summary>
public partial class Program;

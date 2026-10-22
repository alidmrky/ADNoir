using System.Globalization;
using AdNoir.BuildingBlocks.Configuration;
using AdNoir.BuildingBlocks.Context;
using AdNoir.BuildingBlocks.Mongo.Auditing;
using AdNoir.BuildingBlocks.Mongo.Conventions;
using AdNoir.BuildingBlocks.Mongo.Indexing;
using AdNoir.BuildingBlocks.Mongo.Repositories;
using AdNoir.BuildingBlocks.Mongo.Sessions;
using AdNoir.Domain.Abstractions.Repositories;
using AdNoir.IdentityAndOrg.Application.Identity.Abstractions;
using AdNoir.IdentityAndOrg.Domain.Identity;
using AdNoir.IdentityAndOrg.Domain.OrgUnits;
using AdNoir.IdentityAndOrg.Domain.Tenants;
using AdNoir.IdentityAndOrg.Infrastructure.DependencyInjection;
using AdNoir.IdentityAndOrg.Infrastructure.Persistence;
using AdNoir.IdentityAndOrg.Infrastructure.Security;
using AdNoir.IdentityAndOrg.Infrastructure.Seeding;
using AdNoir.Sample.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Serilog;

// =============================================================================
// AdNoir.DbMigrator
// =============================================================================
// Two jobs, in order:
//   1. Create indexes. Not done at application startup, where instances
//      would race, readiness would be delayed, and the app would need write privileges it
//      should not hold in production.
//   2. Run the idempotent seed. An empty database has no users, so nobody
//      can sign in and no protected endpoint can be tried - and nothing reports an error.
//      That silent failure is why seeding is not optional.
//
// Safe to run repeatedly: index creation is a no-op for an index that already exists, and
// every seed step checks before it writes.
// =============================================================================

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddDotEnvFileForDevelopment(builder.Environment);

builder.Services.AddSerilog(configuration => configuration
    .MinimumLevel.Information()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture));

builder.Services.Configure<MongoOptions>(builder.Configuration.GetSection(MongoOptions.SectionName));
builder.Services.Configure<AuditTrailOptions>(builder.Configuration.GetSection(AuditTrailOptions.SectionName));
builder.Services.Configure<SeedOptions>(builder.Configuration.GetSection(SeedOptions.SectionName));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

AdNoirMongoConventions.Register();
IdentityAndOrgClassMaps.Register();
AdNoir.Sample.Infrastructure.Persistence.SampleClassMaps.Register();

builder.Services.AddSingleton<IMongoClient>(provider =>
{
    MongoOptions options = provider.GetRequiredService<IOptions<MongoOptions>>().Value;
    return new MongoClient(options.ConnectionString);
});

builder.Services.AddSingleton(provider =>
{
    MongoOptions options = provider.GetRequiredService<IOptions<MongoOptions>>().Value;
    return provider.GetRequiredService<IMongoClient>().GetDatabase(options.DatabaseName);
});

builder.Services.AddSingleton(TimeProvider.System);

// No request, so the ambient context is supplied by hand: no user, no client, and a tenant
// the seeder sets once it knows which one it created.
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddSingleton<ICurrentUser, SystemCurrentUser>();
builder.Services.AddSingleton<IClientInfo, NullClientInfo>();

#pragma warning disable CS0618 // StandaloneMongoSessionAccessor is the correct choice for the migrator (no broker, no outbox)
builder.Services.AddScoped<IMongoSessionAccessor, StandaloneMongoSessionAccessor>();
#pragma warning restore CS0618
builder.Services.AddScoped<IUnitOfWork, MongoUnitOfWork>();

builder.Services.AddScoped<ITenantRepository, MongoTenantRepository>();
builder.Services.AddScoped<IOrgUnitRepository, MongoOrgUnitRepository>();
builder.Services.AddScoped<IUserRepository, MongoUserRepository>();
builder.Services.AddScoped<IRoleRepository, MongoRoleRepository>();
builder.Services.AddScoped<IOrgUnitSubtreeResolver, OrgUnitSubtreeResolver>();
builder.Services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();

builder.Services.AddScoped<IdentityAndOrgSeeder>();
builder.Services.AddIdentityAndOrgIndexProviders();
builder.Services.AddSampleIndexProviders();
AdNoir.Notification.Infrastructure.DependencyInjection.InfrastructureServiceCollectionExtensions.AddNotificationIndexProviders(builder.Services);

using IHost host = builder.Build();

ILogger<Program> logger = host.Services.GetRequiredService<ILogger<Program>>();
IMongoDatabase database = host.Services.GetRequiredService<IMongoDatabase>();
MongoOptions mongoOptions = host.Services.GetRequiredService<IOptions<MongoOptions>>().Value;

try
{
    IReadOnlyList<IMongoIndexProvider> providers = [.. host.Services.GetServices<IMongoIndexProvider>()];

    int providerCount = providers.Count;
    string databaseName = mongoOptions.DatabaseName;
    logger.LogInformation(
        "Applying {ProviderCount} index providers to database {Database}.",
        providerCount,
        databaseName);

    foreach (IMongoIndexProvider provider in providers)
    {
        string name = provider.GetType().Name;
        logger.LogInformation("Ensuring indexes: {Provider}", name);
        await provider.EnsureIndexesAsync(database, CancellationToken.None);
    }

    logger.LogInformation("All indexes applied. Running seed.");

    await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

    IdentityAndOrgSeeder seeder = scope.ServiceProvider.GetRequiredService<IdentityAndOrgSeeder>();
    SeedResult result = await seeder.SeedAsync(CancellationToken.None);

    bool createdTenant = result.CreatedTenant;
    bool createdAdmin = result.CreatedAdmin;
    bool updatedRoles = result.UpdatedSystemRoles;
    logger.LogInformation(
        "Seed complete. Tenant created: {CreatedTenant}. Administrator created: {CreatedAdmin}. " +
        "System roles updated: {UpdatedRoles}.",
        createdTenant,
        createdAdmin,
        updatedRoles);

    if (result.GeneratedPassword is not null)
    {
        // Written to the console once, and nowhere else. It is not logged through Serilog on
        // purpose: structured logs are shipped, retained and searchable, which is the last
        // place a working administrator password should end up. The account is flagged
        // MustChangePassword so this value stops working at first sign-in.
        Console.WriteLine();
        Console.WriteLine("================================================================");
        Console.WriteLine(" A password was generated for the first administrator.");
        Console.WriteLine(" It is shown ONCE and is not stored anywhere.");
        Console.WriteLine();
        Console.WriteLine($"   password: {result.GeneratedPassword}");
        Console.WriteLine();
        Console.WriteLine(" The account must change it at first sign-in.");
        Console.WriteLine(" Set AdNoir__Seed__SuperAdminPassword to choose it yourself.");
        Console.WriteLine("================================================================");
        Console.WriteLine();
    }

    return 0;
}
catch (MongoException exception)
{
    // The exit code matters: this runs as a deployment step. A silent failure would leave the
    // application either unindexed - correct but slow enough to look like an outage - or
    // unseeded, which means nobody can sign in and nothing reports why.
    logger.LogError(
        exception,
        "Migration failed against database {Database}. The application must not be deployed " +
        "until this succeeds.",
        mongoOptions.DatabaseName);

    return 1;
}
catch (InvalidOperationException exception)
{
    logger.LogError(exception, "Migration failed because of a configuration or data problem.");
    return 1;
}

/// <summary>Entry point marker.</summary>
public partial class Program;

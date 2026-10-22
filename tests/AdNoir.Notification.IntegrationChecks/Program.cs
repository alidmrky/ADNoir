using System.Security.Claims;
using AdNoir.Domain.Abstractions.Repositories;
using AdNoir.Notification.Application.DependencyInjection;
using AdNoir.Notification.Application.Emails;
using Microsoft.AspNetCore.Http;
using AdNoir.BuildingBlocks.Context;
using AdNoir.Notification.Application.Emails.Dtos;
using AdNoir.Notification.Domain.Emails;
using AdNoir.Notification.Infrastructure.Configuration;
using AdNoir.Notification.Infrastructure.DependencyInjection;
using AdNoir.Notification.Infrastructure.Messaging;
using AdNoir.Notification.Infrastructure.Persistence;
using AdNoir.Notification.Infrastructure.Sending;
using AdNoir.Notification.IntegrationChecks;
using AdNoir.Shared.Contracts.Events;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

if (args.Contains("--rabbit", StringComparer.Ordinal) || args.Contains("--rabbit-error", StringComparer.Ordinal))
{
    await BrokerOutboxChecks.RunAsync(args.Contains("--rabbit-error", StringComparer.Ordinal));
    return;
}

string databaseName = "adnoir_notification_check_" + Guid.NewGuid().ToString("N");
Guid tenantId = Guid.CreateVersion7();
Guid disabledTenant = Guid.CreateVersion7();
await using LoopbackSmtpServer smtp = new();
HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = "Development", Args = [] });
builder.Configuration.Sources.Clear();
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["AdNoir:Mongo:ConnectionString"] = "mongodb://localhost:27017/?replicaSet=rs0",
    ["AdNoir:Mongo:DatabaseName"] = databaseName,
    ["AdNoir:Notification:Enabled"] = "true",
    ["AdNoir:Notification:PollSeconds"] = "1",
    ["AdNoir:Notification:MaxAttempts"] = "2",
    ["AdNoir:Notification:Smtp:Host"] = "127.0.0.1",
    ["AdNoir:Notification:Smtp:Port"] = smtp.Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
    ["AdNoir:Notification:Smtp:From"] = "sender@example.test",
    ["AdNoir:Notification:Smtp:EnableSsl"] = "false",
    ["AdNoir:Notification:Smtp:TimeoutSeconds"] = "2",
    [$"AdNoir:Notification:Tenants:{tenantId}:Enabled"] = "true",
    [$"AdNoir:Notification:Tenants:{disabledTenant}:Enabled"] = "false",
});
builder.Logging.ClearProviders();
builder.Logging.AddConsole().SetMinimumLevel(LogLevel.Warning);
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
builder.Services.AddNotificationApplication();
builder.Services.AddNotificationInfrastructure(builder.Configuration, builder.Environment);
// The real MassTransit harness replaces only transport. Use the production consumer pipeline.
builder.Services.AddMassTransitTestHarness(TextWriter.Null, bus =>
{
    bus.SetTestTimeouts(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(2));
    bus.UsingInMemory((context, config) =>
    {
        config.Message<UserRegisteredEvent>(message => message.SetEntityName("identity.user-registered"));
        config.ReceiveEndpoint("notification.user-registered", endpoint =>
            MassTransitConfiguration.ConfigureUserRegisteredEndpoint(context, endpoint));
    });
});
using IHost host = builder.Build();
IMongoClient mongo = host.Services.GetRequiredService<IMongoClient>();
IMongoDatabase database = host.Services.GetRequiredService<IMongoDatabase>();
IMongoCollection<EmailDelivery> collection = database.GetCollection<EmailDelivery>(MongoEmailStore.CollectionName);
ITestHarness harness = host.Services.GetRequiredService<ITestHarness>();
EmailDispatchWorker worker = host.Services.GetServices<IHostedService>().OfType<EmailDispatchWorker>().Single();
try
{
    bool missingIndexRejected = false;
    try { await host.Services.GetRequiredService<EmailQueueGuard>().EnsureReadyAsync(CancellationToken.None); }
    catch (Exception exception) when (exception is MongoCommandException or InvalidOperationException)
    {
        missingIndexRejected = true;
    }
    Check(missingIndexRejected, "missing deduplication index fails closed");
    await new EmailIndexProvider().EnsureIndexesAsync(database);
    await new EmailIndexProvider().EnsureIndexesAsync(database);
    using (var indexCursor = await collection.Indexes.ListAsync())
    {
        var indexes = await indexCursor.ToListAsync();
        Check(indexes.Single(index => index["name"] == "ix_email_tenant_created_id")["key"].AsBsonDocument.Equals(
            MongoDB.Bson.BsonDocument.Parse("{tenantId:1,createdAtUtc:-1,_id:-1}")),
            "list index includes the common repository tie-breaker");
        Check(indexes.Single(index => index["name"] == "ix_email_tenant_status_created_id")["key"].AsBsonDocument.Equals(
            MongoDB.Bson.BsonDocument.Parse("{tenantId:1,status:1,createdAtUtc:-1,_id:-1}")),
            "status list index is idempotent and ordered correctly");
    }
    await harness.Start();
    await worker.StartAsync(CancellationToken.None);
    UserRegisteredEvent welcome = Event("welcome");
    await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => harness.Bus.Publish(welcome, context => context.MessageId = Guid.CreateVersion7())));
    await WaitAsync(async () => (await RowAsync(welcome.EventId))?.Status == EmailDeliveryStatus.Accepted, "SMTP accepted with recorded attempt");
    await harness.Bus.Publish(welcome, context => context.MessageId = Guid.CreateVersion7());
    await WaitAsync(() => Task.FromResult(harness.GetConsumerHarness<UserRegisteredConsumer>().Consumed.Select<UserRegisteredEvent>().Count() >= 7), "consumer handles redelivery");
    Check(await collection.CountDocumentsAsync(row => row.SourceEventId == welcome.EventId) == 1 && smtp.AcceptedCount == 1, "business deduplication prevents a second send");
    UserRegisteredEvent retry = Event("retry");
    await harness.Bus.Publish(retry);
    await WaitAsync(async () => (await RowAsync(retry.EventId))?.Status == EmailDeliveryStatus.RetryScheduled, "SMTP 451 schedules retry");
    await WithStoreAsync(tenantId, async store =>
    {
        EmailDelivery row = (await RowAsync(retry.EventId))!;
        row.NextAttemptAtUtc = DateTime.UtcNow;
        Check(await store.SaveAsync(row, CancellationToken.None), "retry scheduling uses CAS");
    });
    await WaitAsync(async () => (await RowAsync(retry.EventId))?.Status == EmailDeliveryStatus.Accepted, "retry accepted");
    Check((await RowAsync(retry.EventId))!.Attempts.Count == 2, "separate attempt history");
    UserRegisteredEvent rejected = Event("reject");
    await harness.Bus.Publish(rejected);
    await WaitAsync(async () => (await RowAsync(rejected.EventId))?.Status == EmailDeliveryStatus.Failed, "SMTP 550 is terminal");
    UserRegisteredEvent uncertain = Event("uncertain");
    await harness.Bus.Publish(uncertain);
    await WaitAsync(async () => (await RowAsync(uncertain.EventId))?.Status == EmailDeliveryStatus.OutcomeUnknown, "lost SMTP acknowledgement is uncertain");
    UserRegisteredEvent disabled = Event("disabled") with { TenantId = disabledTenant };
    await harness.Bus.Publish(disabled);
    await WaitAsync(async () => (await RowAsync(disabled.EventId))?.Status == EmailDeliveryStatus.Skipped, "disabled tenant is not sent");
    await WithQueriesAsync(disabledTenant, async service =>
    {
        Check((await service.FindAsync((await RowAsync(welcome.EventId))!.Id, CancellationToken.None)).Status == AdNoir.Shared.Contracts.Responses.ResponseStatus.NotFound, "cross-tenant detail hidden");
        var rows = (await service.ListAsync(null, CancellationToken.None)).Data!;
        Check(rows.Count == 1 && rows[0].Attempts.Count == 0 && rows[0].MaskedRecipient is null, "tenant list isolated and skipped recipient omitted");
    });
    await WithQueriesAsync(tenantId, async service =>
    {
        EmailStatisticsDto stats = (await service.StatisticsAsync(CancellationToken.None)).Data!;
        Check(stats.Total == 4 && stats.Attempts == 5 && stats.RetriedMessages == 1 && stats.ByStatus["Accepted"] == 2,
            "server-side counts and attempt statistics");
    });
    await WithQueriesAsync(tenantId, async service =>
    {
        Check((await service.ListAsync(EmailDeliveryStatus.Accepted, CancellationToken.None)).Data!.Count == 2,
            "status filter uses generic repository");
        var detail = (await service.FindAsync((await RowAsync(welcome.EventId))!.Id, CancellationToken.None)).Data!;
        Check(detail.MaskedRecipient == "***@example.test", "application mapping masks recipient");
    });
    await WithQueriesAsync(tenantId, async service =>
    {
        Check((await service.ListAsync(null, CancellationToken.None)).Status == AdNoir.Shared.Contracts.Responses.ResponseStatus.Forbidden &&
            (await service.FindAsync((await RowAsync(welcome.EventId))!.Id, CancellationToken.None)).Status == AdNoir.Shared.Contracts.Responses.ResponseStatus.Forbidden &&
            (await service.StatisticsAsync(CancellationToken.None)).Status == AdNoir.Shared.Contracts.Responses.ResponseStatus.Forbidden,
            "narrow scope rejected for list detail and statistics");
    }, DataScope.Self);
    await WithStoreAsync(tenantId, async store =>
    {
        EmailDelivery first = (await RowAsync(welcome.EventId))!;
        EmailDelivery stale = (await RowAsync(welcome.EventId))!;
        first.RedactAtUtc = DateTime.UtcNow.AddSeconds(-1);
        Check(await store.SaveAsync(first, CancellationToken.None), "retention update succeeds");
        long rejectedVersion = stale.Version;
        Check(!await store.SaveAsync(stale, CancellationToken.None) && stale.Version == rejectedVersion,
            "stale version rejected without mutating the caller version");
        await store.MaintainAsync(DateTime.UtcNow, 30, CancellationToken.None);
    });
    EmailDelivery redacted = (await RowAsync(welcome.EventId))!;
    Check(redacted.IsRedacted && redacted.Body is null && redacted.Recipient is null && redacted.Subject is null, "personal data redacted");
    await harness.Bus.Publish(welcome, context => context.MessageId = Guid.CreateVersion7());
    await WaitAsync(() => Task.FromResult(harness.GetConsumerHarness<UserRegisteredConsumer>().Consumed.Select<UserRegisteredEvent>().Count() >= 12), "redelivery after retention consumed");
    Check(await collection.CountDocumentsAsync(row => row.SourceEventId == welcome.EventId) == 1 && smtp.AcceptedCount == 2, "redacted tombstone still deduplicates");
    await worker.StopAsync(CancellationToken.None);
    UserRegisteredEvent orphan = Event("orphan");
    await harness.Bus.Publish(orphan);
    await WaitAsync(async () => (await RowAsync(orphan.EventId))?.Status == EmailDeliveryStatus.Pending, "orphan fixture queued");
    await WithStoreAsync(tenantId, async store =>
    {
        EmailDelivery claimed = (await store.ClaimAsync(DateTime.UtcNow, 120, true, 30, CancellationToken.None))!;
        claimed.LeaseUntilUtc = DateTime.UtcNow.AddSeconds(-1);
        await store.SaveAsync(claimed, CancellationToken.None);
        await store.MaintainAsync(DateTime.UtcNow, 30, CancellationToken.None);
    });
    Check((await RowAsync(orphan.EventId))!.Status == EmailDeliveryStatus.OutcomeUnknown, "expired worker lease does not resend");
    Guid historyTenant = Guid.CreateVersion7();
    DateTime historyTime = DateTime.UtcNow.AddMinutes(-5);
    EmailDelivery[] history = Enumerable.Range(0, 105).Select(_ => new EmailDelivery
    {
        TenantId = historyTenant,
        SourceEventId = Guid.CreateVersion7(),
        Status = EmailDeliveryStatus.Accepted,
        CreatedAtUtc = historyTime,
        Recipient = "history@example.test",
        RedactAtUtc = historyTime.AddDays(1)
    }).ToArray();
    await collection.InsertManyAsync(history);
    await WithQueriesAsync(historyTenant, async service =>
    {
        var first = (await service.ListAsync(null, CancellationToken.None)).Data!;
        var repeated = (await service.ListAsync(null, CancellationToken.None)).Data!;
        Check(first.Count == 100 && first.Select(row => row.Id).SequenceEqual(repeated.Select(row => row.Id)),
            "latest list bounded to 100 with stable equal-date ordering");
        Guid[] expected = (await collection.Find(row => row.TenantId == historyTenant)
            .SortByDescending(row => row.CreatedAtUtc).ThenByDescending(row => row.Id).Limit(100)
            .ToListAsync()).Select(row => row.Id).ToArray();
        Check(first.Select(row => row.Id).SequenceEqual(expected), "common repository uses unique sort tie-breaker");
        Check((await service.ListAsync(EmailDeliveryStatus.Failed, CancellationToken.None)).Data!.Count == 0,
            "empty status result preserved");
    });
    await using (AsyncServiceScope unresolved = host.Services.CreateAsyncScope())
    {
        bool accessRejected = false;
        try { await unresolved.ServiceProvider.GetRequiredService<IRepository<EmailDelivery>>().FindByIdAsync(history[0].Id); }
        catch (InvalidOperationException) { accessRejected = true; }
        Check(accessRejected, "repository without tenant fails closed");
    }
    await WithStoreAsync(disabledTenant, async store =>
    {
        bool accessRejected = false;
        try { await store.SaveAsync(history[0], CancellationToken.None); }
        catch (InvalidOperationException) { accessRejected = true; }
        Check(accessRejected, "worker cannot write a foreign tenant delivery");
    });
    Console.WriteLine("All notification integration checks passed. No external mail was sent.");
}
finally
{
    await worker.StopAsync(CancellationToken.None);
    await harness.Stop();
    await CleanupAsync(mongo, databaseName);
    Console.WriteLine("Temporary notification database removed.");
}

UserRegisteredEvent Event(string name) => new()
{
    TenantId = tenantId,
    Email = name + "@example.test",
    DisplayName = "Local Check",
    Culture = "tr",
};
Task<EmailDelivery?> RowAsync(Guid eventId) => collection.Find(row => row.SourceEventId == eventId).FirstOrDefaultAsync()!;
async Task WithStoreAsync(Guid tenant, Func<MongoEmailStore, Task> action)
{
    await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
    scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenant, false);
    await action(scope.ServiceProvider.GetRequiredService<MongoEmailStore>());
}
async Task WithQueriesAsync(Guid tenant, Func<IEmailQueryService, Task> action, DataScope readScope = DataScope.Tenant)
{
    await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
    scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenant, true);
    IHttpContextAccessor context = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
    context.HttpContext = new DefaultHttpContext
    {
        User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(AdNoirClaimTypes.Subject, Guid.CreateVersion7().ToString()),
            new Claim(AdNoirClaimTypes.ReadScope, readScope.ToString())], "integration"))
    };
    try { await action(scope.ServiceProvider.GetRequiredService<IEmailQueryService>()); }
    finally { context.HttpContext = null; }
}
static void Check(bool success, string label)
{
    if (!success) { throw new InvalidOperationException(label); }
    Console.WriteLine("PASS " + label);
}
static async Task WaitAsync(Func<Task<bool>> condition, string label)
{
    using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
    while (!await condition()) { await Task.Delay(100, timeout.Token); }
    Console.WriteLine("PASS " + label);
}

static async Task CleanupAsync(IMongoClient mongo, string name)
{
    if (!name.StartsWith("adnoir_notification_check_", StringComparison.Ordinal) || name.Length != 58)
    {
        throw new InvalidOperationException("Refusing unexpected database cleanup target.");
    }
    await mongo.DropDatabaseAsync(name);
}

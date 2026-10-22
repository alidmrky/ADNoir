using AdNoir.BuildingBlocks.Context;
using AdNoir.BuildingBlocks.Mongo.Repositories;
using AdNoir.Domain.Abstractions.Repositories;
using AdNoir.Notification.Domain.Emails;
using AdNoir.BuildingBlocks.Mongo.DependencyInjection;
using AdNoir.BuildingBlocks.Mongo.Sessions;
using AdNoir.Notification.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using AdNoir.Notification.Infrastructure.Messaging;
using AdNoir.Notification.Infrastructure.Persistence;
using AdNoir.Shared.Contracts.Events;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using RabbitMQ.Client;

namespace AdNoir.Notification.IntegrationChecks;

internal static class BrokerOutboxChecks
{
    internal static async Task RunAsync(bool errorQueue = false)
    {
        ushort port = errorQueue ? (ushort)16672 : (ushort)5672;
        string suffix = Guid.NewGuid().ToString("N");
        string databaseName = "adnoir_broker_check_" + suffix;
        string queue = "adnoir-check-" + suffix;
        string exchange = queue + "-registered";
        Guid tenant = Guid.CreateVersion7();
        // Local default credentials only. Do not read or modify the application's environment file.
        var factory = new ConnectionFactory { HostName = "localhost", Port = port, UserName = "guest", Password = "guest" };
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = "Development", Args = [] });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AdNoir:Mongo:ConnectionString"] = "mongodb://localhost:27017/?replicaSet=rs0",
            ["AdNoir:Mongo:DatabaseName"] = databaseName,
            ["AdNoir:Notification:Enabled"] = "false",
        });
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole().SetMinimumLevel(LogLevel.Error);
        builder.Services.AddScoped<ITenantContext, TenantContext>();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
        builder.Services.AddAdNoirMongo(builder.Configuration);
        NotificationClassMaps.Register();
        builder.Services.AddSingleton(new NotificationOptions());
        builder.Services.AddSingleton<EmailQueueGuard>();
        builder.Services.AddScoped<MongoEmailStore>();
        builder.Services.Replace(ServiceDescriptor.Scoped<IMongoSessionAccessor, MassTransitMongoSessionAccessor>());
        builder.Services.Replace(ServiceDescriptor.Scoped<IUnitOfWork, MassTransitMongoUnitOfWork>());
        builder.Services.AddScoped<IRepository<BrokerProbeEntity>, MongoRepository<BrokerProbeEntity>>();
        builder.Services.AddMassTransitTestHarness(TextWriter.Null, bus =>
        {
            bus.AddConsumer<UserRegisteredConsumer>();
            bus.AddMongoDbOutbox(outbox =>
            {
                outbox.ClientFactory(provider => provider.GetRequiredService<IMongoClient>());
                outbox.DatabaseFactory(provider => provider.GetRequiredService<IMongoDatabase>());
                outbox.UseBusOutbox();
            });
            bus.SetTestTimeouts(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(2));
            bus.UsingRabbitMq((context, config) =>
            {
                config.Host("localhost", port, "/", host => { host.Username("guest"); host.Password("guest"); });
                config.Message<UserRegisteredEvent>(message => message.SetEntityName(exchange));
                config.Publish<IntegrationEvent>(publish => publish.Exclude = true);
                config.Publish<IIntegrationEvent>(publish => publish.Exclude = true);
                config.ReceiveEndpoint(queue, endpoint =>
                {
                    endpoint.SetQueueArgument("x-queue-type", "quorum");
                    MassTransitConfiguration.ConfigureUserRegisteredEndpoint(context, endpoint);
                });
            });
        });
        using var host = builder.Build();
        var mongo = host.Services.GetRequiredService<IMongoClient>();
        var database = host.Services.GetRequiredService<IMongoDatabase>();
        var emails = database.GetCollection<EmailDelivery>(MongoEmailStore.CollectionName);
        var business = database.GetCollection<BrokerProbeEntity>(MongoCollectionNaming.For(typeof(BrokerProbeEntity)));
        var harness = host.Services.GetRequiredService<ITestHarness>();
        try
        {
            await new EmailIndexProvider().EnsureIndexesAsync(database);
            await host.StartAsync();
            UserRegisteredEvent committed = NewEvent();
            UserRegisteredEvent aborted = NewEvent();
            await PublishInTransaction(committed, commit: true);
            await WaitAsync(async () => await emails.CountDocumentsAsync(row => row.SourceEventId == committed.EventId) == 1);
            Check(await business.CountDocumentsAsync(row => row.Id == committed.EventId) == 1,
                "committed business data and outbox event delivered through RabbitMQ");
            await PublishInTransaction(aborted, commit: false);
            Check(await business.CountDocumentsAsync(row => row.Id == aborted.EventId) == 0,
                "rollback removes business data");
            // A later committed event is a delivery barrier, followed by a bounded observation window.
            var barrier = NewEvent();
            await PublishInTransaction(barrier, commit: true);
            await WaitAsync(async () => await emails.CountDocumentsAsync(row => row.SourceEventId == barrier.EventId) == 1);
            await Task.Delay(TimeSpan.FromSeconds(3));
            Check(await emails.CountDocumentsAsync(row => row.SourceEventId == aborted.EventId) == 0,
                "rolled-back event was not consumed");
            await harness.Bus.Publish(committed, context => context.MessageId = Guid.CreateVersion7());
            await harness.Bus.Publish(committed, context => context.MessageId = Guid.CreateVersion7());
            await WaitAsync(() => Task.FromResult(harness.GetConsumerHarness<UserRegisteredConsumer>().Consumed
                .Select<UserRegisteredEvent>().Count() >= 4));
            Check(await emails.CountDocumentsAsync(row => row.SourceEventId == committed.EventId) == 1,
                "different transport message IDs preserve business idempotency");
            Check(await emails.CountDocumentsAsync(row => row.Status != EmailDeliveryStatus.Skipped) == 0,
                "disabled tenant never queues SMTP delivery");
            if (errorQueue)
            {
                // The full production retry policy stays enabled; do not shorten it to make the check pass.
                var invalid = NewEvent() with { Email = "invalid-address" };
                await harness.Bus.Publish(invalid);
                using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(4));
                BasicGetResult? failure = null;
                while (failure is null)
                {
                    // Passive lookup may close its channel until retry exhaustion creates the error queue.
                    await using var probe = await connection.CreateChannelAsync(cancellationToken: timeout.Token);
                    try
                    {
                        await probe.QueueDeclarePassiveAsync(queue + "_error", timeout.Token);
                        failure = await probe.BasicGetAsync(queue + "_error", true, timeout.Token);
                    }
                    catch (RabbitMQ.Client.Exceptions.OperationInterruptedException exception) when (exception.ShutdownReason?.ReplyCode == 404)
                    {
                        // Queue has not been created yet; the production retry policy is still running.
                    }
                    if (failure is null) { await Task.Delay(2000, timeout.Token); }
                }
                Check(failure is not null && failure.BasicProperties.Headers?.ContainsKey("MT-Fault-ExceptionType") == true,
                    "production retry exhaustion routes poison event to error queue with fault metadata");
                Check(await emails.CountDocumentsAsync(row => row.SourceEventId == invalid.EventId) == 0,
                    "poison event leaves no notification business data");
            }
            Console.WriteLine("PASS real RabbitMQ outbox checks; no SMTP invoked.");
        }
        finally
        {
            try
            {
                await host.StopAsync();
                await channel.QueueDeleteAsync(queue, false, false);
                await channel.QueueDeleteAsync(queue + "_error", false, false);
                await channel.QueueDeleteAsync(queue + "_skipped", false, false);
                await channel.ExchangeDeleteAsync(queue, false);
                await channel.ExchangeDeleteAsync(queue + "_error", false);
                await channel.ExchangeDeleteAsync(queue + "_skipped", false);
                await channel.ExchangeDeleteAsync(exchange, false);
            }
            finally
            {
                await mongo.DropDatabaseAsync(databaseName);
            }
            Console.WriteLine("Temporary broker topology and Mongo database removed.");
        }

        UserRegisteredEvent NewEvent() => new() { TenantId = tenant, Email = "fixture@example.test", DisplayName = "Fixture" };

        async Task PublishInTransaction(UserRegisteredEvent message, bool commit)
        {
            await using var scope = host.Services.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenant, false);
            var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await unit.BeginAsync();
            await scope.ServiceProvider.GetRequiredService<IRepository<BrokerProbeEntity>>()
                .InsertAsync(new BrokerProbeEntity { Id = message.EventId, TenantId = tenant });
            await scope.ServiceProvider.GetRequiredService<IPublishEndpoint>().Publish(message);
            if (commit) { await unit.CommitAsync(); }
            else { await unit.RollbackAsync(); }
        }
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) { throw new InvalidOperationException(label); }
        Console.WriteLine("PASS " + label);
    }

    private static async Task WaitAsync(Func<Task<bool>> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (!await predicate()) { await Task.Delay(100, timeout.Token); }
    }
}

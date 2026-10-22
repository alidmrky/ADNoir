using AdNoir.Notification.Infrastructure.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AdNoir.Notification.Infrastructure.Persistence;

/// <summary>Refuses readiness when migrations needed for durable deduplication are missing.</summary>
public sealed class NotificationReadiness(IMongoDatabase database) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: cancellationToken);
            using var cursor = await database.GetCollection<BsonDocument>(MongoEmailStore.CollectionName)
                .Indexes.ListAsync(cancellationToken);
            var indexes = await cursor.ToListAsync(cancellationToken);
            bool unique = indexes.Any(index => index["name"] == "ux_email_event_kind" && index.GetValue("unique", false).AsBoolean);
            return unique ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("notification_migration_required");
        }
        catch (MongoException)
        {
            return HealthCheckResult.Unhealthy("notification_storage_unavailable");
        }
    }
}

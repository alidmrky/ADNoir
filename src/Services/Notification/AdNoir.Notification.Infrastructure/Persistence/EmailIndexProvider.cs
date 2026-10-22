using AdNoir.BuildingBlocks.Mongo.Indexing;
using AdNoir.Notification.Domain.Emails;
using AdNoir.Notification.Infrastructure.Configuration;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace AdNoir.Notification.Infrastructure.Persistence;

/// <summary>Notification indexes, created only by DbMigrator.</summary>
public sealed class EmailIndexProvider : IMongoIndexProvider
{
    /// <inheritdoc />
    public async Task EnsureIndexesAsync(IMongoDatabase database, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);
        NotificationClassMaps.Register();
        var keys = Builders<EmailDelivery>.IndexKeys;
        await database.GetCollection<EmailDelivery>(MongoEmailStore.CollectionName).Indexes.CreateManyAsync([
            new(keys.Ascending(x => x.TenantId).Ascending(x => x.SourceEventId).Ascending(x => x.Kind),
                new CreateIndexOptions { Name = "ux_email_event_kind", Unique = true }),
            new(keys.Ascending(x => x.TenantId).Descending(x => x.CreatedAtUtc),
                new CreateIndexOptions { Name = "ix_email_tenant_created" }),
            new(keys.Ascending(x => x.TenantId).Descending(x => x.CreatedAtUtc).Descending(x => x.Id),
                new CreateIndexOptions { Name = "ix_email_tenant_created_id" }),
            new(keys.Ascending(x => x.TenantId).Ascending(x => x.Status).Descending(x => x.CreatedAtUtc).Descending(x => x.Id),
                new CreateIndexOptions { Name = "ix_email_tenant_status_created_id" }),
            new(keys.Ascending(x => x.TenantId).Ascending(x => x.Status).Ascending(x => x.NextAttemptAtUtc),
                new CreateIndexOptions { Name = "ix_email_due" }),
            new(keys.Ascending(x => x.TenantId).Ascending(x => x.Status).Ascending(x => x.LeaseUntilUtc),
                new CreateIndexOptions { Name = "ix_email_lease" }),
            new(keys.Ascending(x => x.TenantId).Ascending(x => x.IsRedacted).Ascending(x => x.RedactAtUtc),
                new CreateIndexOptions { Name = "ix_email_retention" }),
        ], cancellationToken);
    }
}

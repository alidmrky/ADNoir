using MongoDB.Bson;
using MongoDB.Driver;

namespace AdNoir.Notification.Infrastructure.Persistence;

/// <summary>Checks the business deduplication index before accepting or sending work.</summary>
public sealed class EmailQueueGuard(IMongoDatabase database)
{
    /// <summary>Fails closed when the migrator has not installed the exact unique index.</summary>
    public async Task EnsureReadyAsync(CancellationToken cancellationToken)
    {
        using var cursor = await database.GetCollection<BsonDocument>(MongoEmailStore.CollectionName).Indexes.ListAsync(cancellationToken);
        var indexes = await cursor.ToListAsync(cancellationToken);
        BsonDocument expected = new() { { "tenantId", 1 }, { "sourceEventId", 1 }, { "kind", 1 } };
        if (!indexes.Any(index => index.GetValue("unique", false).AsBoolean && index["key"].AsBsonDocument.Equals(expected)))
        {
            throw new InvalidOperationException("Notification migration is required before processing email.");
        }
    }
}

using AdNoir.BuildingBlocks.Mongo.Indexing;
using AdNoir.BuildingBlocks.Mongo.Repositories;
using AdNoir.Sample.Domain.SampleItems;
using MongoDB.Driver;

namespace AdNoir.Sample.Infrastructure.Persistence.Indexes;

/// <summary>
/// Indexes for the sample_items collection (indexes in DbMigrator, not startup).
/// </summary>
public sealed class SampleItemIndexProvider : IMongoIndexProvider
{
    /// <inheritdoc />
    public async Task EnsureIndexesAsync(IMongoDatabase database, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);

        IMongoCollection<SampleItem> collection =
            database.GetCollection<SampleItem>(MongoCollectionNaming.For(typeof(SampleItem)));

        // Tenant-based listing: the most common query shape.
        CreateIndexModel<SampleItem> tenantCreatedAt = new(
            Builders<SampleItem>.IndexKeys
                .Ascending(i => i.TenantId)
                .Descending(i => i.CreatedAtUtc),
            new CreateIndexOptions { Name = "ix_sample_item_tenant_created" });

        await collection.Indexes.CreateManyAsync([tenantCreatedAt], cancellationToken);
    }
}

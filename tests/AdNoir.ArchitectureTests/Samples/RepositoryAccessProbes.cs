using AdNoir.Domain.Abstractions.Repositories;
using AdNoir.Sample.Domain.SampleItems;
using MongoDB.Driver;

namespace AdNoir.ArchitectureTests.Samples;

internal static class RepositoryAccessProbes
{
    internal static object DirectRead(IMongoCollection<SampleItem> collection) => collection.Find(Builders<SampleItem>.Filter.Empty);

    internal static async Task AsyncWrite(IMongoCollection<SampleItem> collection, SampleItem item)
    {
        await collection.InsertOneAsync(item);
    }

    internal static Action<SampleItem, InsertOneOptions, CancellationToken> DelegateWrite(IMongoCollection<SampleItem> collection)
        => collection.InsertOne;

    internal static object Indexes(IMongoCollection<SampleItem> collection) => collection.Indexes;

    internal static object SharedRead(IRepository<SampleItem> repository, Guid id)
        => repository.FindByIdAsync(id, CancellationToken.None);
}

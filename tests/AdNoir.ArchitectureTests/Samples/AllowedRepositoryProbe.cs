using AdNoir.BuildingBlocks.Context;
using AdNoir.BuildingBlocks.Mongo.Repositories;
using AdNoir.BuildingBlocks.Mongo.Sessions;
using AdNoir.Domain.Abstractions.Repositories;
using AdNoir.Sample.Domain.SampleItems;
using MongoDB.Driver;

namespace AdNoir.ArchitectureTests.Samples;

internal sealed class AllowedRepositoryProbe(
    IMongoDatabase database, IMongoSessionAccessor sessions, ITenantContext tenant,
    ICurrentUser user, TimeProvider clock) : MongoRepository<SampleItem>(database, sessions, tenant, user, clock)
{
    internal async Task<long> ScopedCount(CancellationToken cancellationToken)
    {
        var filter = await BuildScopeFilterAsync(cancellationToken: cancellationToken);
        var session = await SessionAccessor.GetSessionAsync(cancellationToken);
        return await Collection.CountDocumentsAsync(session, filter, cancellationToken: cancellationToken);
    }
}

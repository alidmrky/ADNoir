using AdNoir.BuildingBlocks.Context;
using AdNoir.BuildingBlocks.Mongo.Repositories;
using AdNoir.BuildingBlocks.Mongo.Sessions;
using AdNoir.Sample.Domain.SampleItems;
using MongoDB.Driver;

namespace AdNoir.Sample.Infrastructure.Persistence;

/// <summary>MongoDB implementation of <see cref="ISampleItemRepository"/>.</summary>
public sealed class MongoSampleItemRepository(
    IMongoDatabase database,
    IMongoSessionAccessor sessionAccessor,
    ITenantContext tenantContext,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : MongoRepository<SampleItem>(database, sessionAccessor, tenantContext, currentUser, timeProvider),
      ISampleItemRepository
{
    // All required CRUD is supplied by MongoRepository<T>. This class exists so the
    // repository interface lives in Domain while the Mongo implementation stays in
    // Infrastructure.
}

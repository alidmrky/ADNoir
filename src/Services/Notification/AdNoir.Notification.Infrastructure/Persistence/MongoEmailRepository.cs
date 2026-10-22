using AdNoir.BuildingBlocks.Context;
using AdNoir.BuildingBlocks.Mongo.Repositories;
using AdNoir.BuildingBlocks.Mongo.Sessions;
using AdNoir.Domain.Abstractions.Repositories;
using AdNoir.Notification.Application.Emails.Abstractions;
using AdNoir.Notification.Application.Emails.Dtos;
using AdNoir.Notification.Domain.Emails;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AdNoir.Notification.Infrastructure.Persistence;

/// <summary>Standard entity reads and scoped server-side email statistics.</summary>
public sealed class MongoEmailRepository(IMongoDatabase database, IMongoSessionAccessor sessions,
    ITenantContext tenant, ICurrentUser currentUser, TimeProvider timeProvider)
    : MongoRepository<EmailDelivery>(database, sessions, tenant, currentUser, timeProvider,
        collectionName: MongoEmailStore.CollectionName), IEmailQueries
{
    /// <inheritdoc />
    public async Task<EmailStatisticsDto> StatisticsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
    {
        var range = await BuildScopeFilterAsync(cancellationToken: cancellationToken) & Filter.Gte(x => x.CreatedAtUtc, fromUtc) & Filter.Lt(x => x.CreatedAtUtc, toUtc);
        // Server-side aggregation keeps memory bounded even for a busy tenant.
        BsonDocument stage = BsonDocument.Parse("""
        { "$group": {
          "_id": "$status", "count": { "$sum": 1 },
          "attempts": { "$sum": { "$size": "$attempts" } },
          "retried": { "$sum": { "$cond": [{ "$gt": [{ "$size": "$attempts" }, 1] }, 1, 0] } },
          "queueSum": { "$sum": { "$cond": [{ "$gt": [{ "$size": "$attempts" }, 0] },
              { "$subtract": [{ "$arrayElemAt": ["$attempts.startedAtUtc", 0] }, "$createdAtUtc"] }, 0] } },
          "queueCount": { "$sum": { "$cond": [{ "$gt": [{ "$size": "$attempts" }, 0] }, 1, 0] } },
          "durationSum": { "$sum": { "$sum": "$attempts.durationMilliseconds" } },
          "durationCount": { "$sum": { "$size": { "$filter": { "input": "$attempts", "as": "a",
              "cond": { "$ne": [{ "$ifNull": ["$$a.durationMilliseconds", null] }, null] } } } } }
        } }
        """);
        IClientSessionHandle session = await SessionAccessor.GetSessionAsync(cancellationToken);
        var groups = await Collection.Aggregate(session, new AggregateOptions { MaxTime = TimeSpan.FromSeconds(10) })
            .Match(range).AppendStage<BsonDocument>(stage).ToListAsync(cancellationToken);
        double queueCount = groups.Sum(x => x["queueCount"].ToDouble());
        double durationCount = groups.Sum(x => x["durationCount"].ToDouble());
        return new(fromUtc, toUtc, groups.Sum(x => x["count"].ToInt64()),
            groups.ToDictionary(x => x["_id"].AsString, x => x["count"].ToInt64(), StringComparer.Ordinal),
            groups.Sum(x => x["attempts"].ToInt64()), groups.Sum(x => x["retried"].ToInt64()),
            queueCount == 0 ? null : groups.Sum(x => x["queueSum"].ToDouble()) / queueCount,
            durationCount == 0 ? null : groups.Sum(x => x["durationSum"].ToDouble()) / durationCount);
    }

}

using MongoDB.Driver;

namespace AdNoir.BuildingBlocks.Mongo.Sessions;

/// <summary>
/// Supplies the MongoDB session that repositories must pass to every write.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Getting this wrong fails silently.</strong>
/// The transactional outbox and our repositories have to share one
/// <c>IClientSessionHandle</c>. The session is owned by MassTransit's
/// <c>MongoDbContext</c> and we take it from there. A session opened by our own call to
/// <c>IMongoClient.StartSession()</c> does <em>not</em> cover the outbox: the data change and
/// the outbox record land in separate transactions, atomicity is lost, and nothing reports an
/// error - the system appears to work.
/// </para>
/// <para>
/// The abstraction is here so that swapping the session source is a registration change
/// rather than a rewrite of every repository. A rollback check against the real transport is
/// what proves the sharing actually happens, and it is the only thing standing between a
/// working outbox and a broken one that looks fine.
/// </para>
/// </remarks>
public interface IMongoSessionAccessor
{
    /// <summary>Whether a session has been started for this scope.</summary>
    bool HasSession { get; }

    /// <summary>Gets the session for this scope, starting one if necessary.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The session that all writes in this scope must use.</returns>
    ValueTask<IClientSessionHandle> GetSessionAsync(CancellationToken cancellationToken = default);
}

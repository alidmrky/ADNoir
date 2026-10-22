using MassTransit.MongoDbIntegration;
using MongoDB.Driver;

namespace AdNoir.BuildingBlocks.Mongo.Sessions;

/// <summary>
/// Borrows the MongoDB session that MassTransit opened for the transactional outbox.
/// </summary>
/// <remarks>
/// <para>
/// This is the production implementation of <see cref="IMongoSessionAccessor"/>, active
/// whenever a transactional outbox is registered. The session it supplies is the same one
/// MassTransit's outbox writes through, which is what makes data changes and outbox records
/// atomic. Using any other session — including one opened by our own <c>IMongoClient</c> —
/// would let them commit in separate transactions and lose the atomicity guarantee without
/// producing any error (AGENTS.md §8.1).
/// </para>
/// <para>
/// Ownership: MassTransit owns the session lifecycle. We never call <c>StartSession</c> or
/// <c>CommitTransaction</c> directly; <see cref="MassTransitMongoUnitOfWork"/> calls
/// <see cref="MongoDbContext.StartSession"/> and delegates commit/abort back to MassTransit's
/// context. This class is just the accessor bridge.
/// </para>
/// <para>
/// A rollback check against the real transport is the only proof that this wiring is correct.
/// If it fails, the session is not shared and atomicity has been lost silently.
/// </para>
/// </remarks>
public sealed class MassTransitMongoSessionAccessor(MongoDbContext massTransitContext)
    : IMongoSessionAccessor
{
    private readonly MongoDbContext _context = massTransitContext;
    private IClientSessionHandle? _session;

    /// <inheritdoc />
    public bool HasSession => _session is not null;

    /// <inheritdoc />
    /// <remarks>
    /// Retrieves the session from MassTransit's <see cref="MongoDbContext"/>. The context
    /// manages the session lifetime; we only read it.
    /// </remarks>
    public async ValueTask<IClientSessionHandle> GetSessionAsync(CancellationToken cancellationToken = default)
    {
        if (_session is not null)
        {
            return _session;
        }

        // MassTransit opens the session. Transaction lifecycle is handled by the unit of work; we take the handle it
        // already owns. Calling StartSession ourselves would open a second, unrelated session.
        _session = await _context.StartSession(cancellationToken);
        return _session;
    }
}


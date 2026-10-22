using AdNoir.Domain.Abstractions.Repositories;
using MongoDB.Driver;

namespace AdNoir.BuildingBlocks.Mongo.Sessions;

/// <summary>
/// Transaction control over the shared MongoDB session.
/// </summary>
/// <remarks>
/// Deliberately does not start its own session - it asks <see cref="IMongoSessionAccessor"/>
/// for one, which is what makes the outbox and the repositories share a transaction. See the
/// remarks on <see cref="IMongoSessionAccessor"/> for why that direction matters.
/// </remarks>
public sealed class MongoUnitOfWork(IMongoSessionAccessor sessionAccessor) : IUnitOfWork
{
    private readonly IMongoSessionAccessor _sessionAccessor = sessionAccessor;
    private IClientSessionHandle? _session;

    /// <inheritdoc />
    public bool HasActiveTransaction => _session?.IsInTransaction ?? false;

    /// <inheritdoc />
    public async Task BeginAsync(CancellationToken cancellationToken = default)
    {
        _session = await _sessionAccessor.GetSessionAsync(cancellationToken);

        if (_session.IsInTransaction)
        {
            return;
        }

        _session.StartTransaction();
    }

    /// <inheritdoc />
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_session is null || !_session.IsInTransaction)
        {
            throw new InvalidOperationException(
                "There is no open transaction to commit. Call BeginAsync first.");
        }

        await _session.CommitTransactionAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_session is null || !_session.IsInTransaction)
        {
            return;
        }

        await _session.AbortTransactionAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        // An open transaction at disposal means the request failed somewhere between Begin
        // and Commit. Aborting is the safe reading: a half-applied change must not be left
        // to be committed by accident later.
        if (_session is { IsInTransaction: true })
        {
            await _session.AbortTransactionAsync(CancellationToken.None);
        }

        _session = null;
    }
}

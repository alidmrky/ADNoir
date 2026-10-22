namespace AdNoir.Domain.Abstractions.Repositories;

/// <summary>
/// A transactional scope shared by every repository resolved in the same request.
/// </summary>
/// <remarks>
/// <para>
/// The implementation does <strong>not</strong> open its own database session. It wraps the
/// session owned by MassTransit's outbox, and repositories take the session from here. This
/// direction is not negotiable: a session we opened ourselves would not cover the outbox, so
/// the data change and the outbox record would land in different transactions. Atomicity
/// would be lost <em>with no error at all</em> — everything would appear to work.
/// </para>
/// <para>
/// Because of this, any event publishing must happen before <see cref="CommitAsync"/>;
/// publishing afterwards writes the message outside the transaction.
/// </para>
/// <para>
/// Multi-document transactions require a MongoDB replica set, which is why one is mandatory
/// even for a single node.
/// </para>
/// </remarks>
public interface IUnitOfWork : IAsyncDisposable
{
    /// <summary>Whether a transaction is currently open.</summary>
    bool HasActiveTransaction { get; }

    /// <summary>Opens a transaction. Calling this while one is already open is a no-op.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes once the transaction is open.</returns>
    Task BeginAsync(CancellationToken cancellationToken = default);

    /// <summary>Commits the open transaction, publishing any outbox messages with it.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes once the transaction is committed.</returns>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>Aborts the open transaction, discarding data changes and outbox records alike.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes once the transaction is aborted.</returns>
    Task RollbackAsync(CancellationToken cancellationToken = default);
}

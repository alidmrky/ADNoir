using AdNoir.Domain.Abstractions.Repositories;
using MassTransit.MongoDbIntegration;

namespace AdNoir.BuildingBlocks.Mongo.Sessions;

/// <summary>Delegates transaction lifecycle to the context shared with the transactional outbox.</summary>
public sealed class MassTransitMongoUnitOfWork(MongoDbContext context) : IUnitOfWork
{
    /// <inheritdoc />
    public bool HasActiveTransaction => context.Session?.IsInTransaction ?? false;

    /// <inheritdoc />
    public Task BeginAsync(CancellationToken cancellationToken = default) =>
        context.BeginTransaction(cancellationToken);

    /// <inheritdoc />
    public Task CommitAsync(CancellationToken cancellationToken = default) =>
        context.CommitTransaction(cancellationToken);

    /// <inheritdoc />
    public Task RollbackAsync(CancellationToken cancellationToken = default) =>
        HasActiveTransaction ? context.AbortTransaction(cancellationToken) : Task.CompletedTask;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        // The DI scope owns the context and its session; only abort unfinished work here.
        await RollbackAsync(CancellationToken.None);
    }
}

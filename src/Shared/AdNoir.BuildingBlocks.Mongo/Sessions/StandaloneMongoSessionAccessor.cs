using MongoDB.Driver;

namespace AdNoir.BuildingBlocks.Mongo.Sessions;

/// <summary>
/// Opens and owns its own MongoDB session.
/// </summary>
/// <remarks>
/// <para>
/// Used when RabbitMQ is not configured (development without a broker). When RabbitMQ is
/// present, <see cref="MassTransitMongoSessionAccessor"/> is registered instead, which shares
/// the session with the transactional outbox and preserves atomicity (AGENTS.md §8.1).
/// </para>
/// <para>
/// A session started here does not cover MassTransit's outbox collection. If this class is
/// accidentally left registered in an environment that uses the outbox, data and messages
/// will commit in separate transactions — silently, without any error.
/// A rollback check against the real transport catches this.
/// </para>
/// </remarks>
[Obsolete("Active only when RabbitMQ is not configured. " +
    "MassTransitMongoSessionAccessor is registered automatically when a RabbitMQ host is present.")]
public sealed class StandaloneMongoSessionAccessor(IMongoClient client) : IMongoSessionAccessor, IAsyncDisposable
{
    private readonly IMongoClient _client = client;
    private IClientSessionHandle? _session;

    /// <inheritdoc />
    public bool HasSession => _session is not null;

    /// <inheritdoc />
    public async ValueTask<IClientSessionHandle> GetSessionAsync(CancellationToken cancellationToken = default)
    {
        _session ??= await _client.StartSessionAsync(cancellationToken: cancellationToken);
        return _session;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _session?.Dispose();
        _session = null;

        return ValueTask.CompletedTask;
    }
}

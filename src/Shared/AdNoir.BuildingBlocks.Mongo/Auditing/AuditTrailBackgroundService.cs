using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace AdNoir.BuildingBlocks.Mongo.Auditing;

/// <summary>
/// Drains the audit queue into MongoDB in batches.
/// </summary>
public sealed class AuditTrailBackgroundService(
    ChannelAuditTrailWriter writer,
    IMongoDatabase database,
    IOptions<AuditTrailOptions> options,
    ILogger<AuditTrailBackgroundService> logger) : BackgroundService
{
    private readonly ChannelAuditTrailWriter _writer = writer;
    private readonly IMongoCollection<AuditLog> _collection =
        database.GetCollection<AuditLog>(options.Value.CollectionName);

    private readonly AuditTrailOptions _options = options.Value;
    private readonly ILogger<AuditTrailBackgroundService> _logger = logger;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        List<AuditLog> batch = new(_options.BatchSize);

        try
        {
            while (await _writer.Reader.WaitToReadAsync(stoppingToken))
            {
                batch.Clear();

                while (batch.Count < _options.BatchSize && _writer.Reader.TryRead(out AuditLog? entry))
                {
                    batch.Add(entry);
                }

                if (batch.Count > 0)
                {
                    await FlushAsync(batch, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }

        await DrainRemainingAsync();
    }

    private async Task FlushAsync(List<AuditLog> batch, CancellationToken cancellationToken)
    {
        try
        {
            // No session and no transaction on purpose. Audit writes must not join the
            // caller's transaction: an audit failure would then roll back the very change it
            // was recording, and a long-running audit write would hold that transaction open.
            await _collection.InsertManyAsync(
                batch,
                new InsertManyOptions { IsOrdered = false },
                cancellationToken);
        }
        catch (MongoException exception)
        {
            // Losing audit records is bad; taking the application down over it is worse.
            // Log loudly instead - a spike here is what an alert should watch for.
            _logger.LogError(
                exception,
                "Failed to persist {Count} audit records. They are lost.",
                batch.Count);
        }
    }

    private async Task DrainRemainingAsync()
    {
        List<AuditLog> remaining = [];

        while (_writer.Reader.TryRead(out AuditLog? entry))
        {
            remaining.Add(entry);
        }

        if (remaining.Count == 0)
        {
            return;
        }

        using CancellationTokenSource shutdownTimeout = new(TimeSpan.FromSeconds(5));
        await FlushAsync(remaining, shutdownTimeout.Token);
    }
}

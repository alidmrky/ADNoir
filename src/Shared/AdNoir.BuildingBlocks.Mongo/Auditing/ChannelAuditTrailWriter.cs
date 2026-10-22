using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace AdNoir.BuildingBlocks.Mongo.Auditing;

/// <summary>Queues audit records for background persistence.</summary>
public interface IAuditTrailWriter
{
    /// <summary>Queues a record without waiting for it to be stored.</summary>
    /// <param name="entry">The record to store.</param>
    /// <returns><see langword="false"/> when the queue is full and the entry was dropped.</returns>
    bool TryWrite(AuditLog entry);
}

/// <summary>
/// In-memory queue in front of the audit collection.
/// </summary>
/// <remarks>
/// <para>
/// Writing audit records inline would add a round trip to every write the user is waiting
/// on, so records go through a bounded channel and a background service drains it.
/// </para>
/// <para>
/// The channel drops the oldest entry when full rather than blocking. That is a real
/// trade-off: under sustained overload some audit records are lost. Blocking would be worse -
/// it would turn a full audit queue into an outage of the thing being audited. The drop is
/// counted so it can be alerted on rather than discovered later.
/// </para>
/// </remarks>
public sealed class ChannelAuditTrailWriter : IAuditTrailWriter
{
    private readonly Channel<AuditLog> _channel;
    private long _droppedCount;

    /// <summary>Initializes a new instance.</summary>
    /// <param name="options">Audit trail settings.</param>
    public ChannelAuditTrailWriter(IOptions<AuditTrailOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _channel = Channel.CreateBounded<AuditLog>(
            new BoundedChannelOptions(options.Value.QueueCapacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false,
            });
    }

    /// <summary>Reader used by the background service.</summary>
    public ChannelReader<AuditLog> Reader => _channel.Reader;

    /// <summary>How many records have been dropped because the queue was full.</summary>
    public long DroppedCount => Interlocked.Read(ref _droppedCount);

    /// <inheritdoc />
    public bool TryWrite(AuditLog entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (_channel.Writer.TryWrite(entry))
        {
            return true;
        }

        Interlocked.Increment(ref _droppedCount);
        return false;
    }
}

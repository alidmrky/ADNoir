using AdNoir.BuildingBlocks.Context;
using AdNoir.BuildingBlocks.Mongo.Sessions;
using AdNoir.BuildingBlocks.Mongo.Repositories;
using AdNoir.Domain.Abstractions.Repositories;
using AdNoir.Domain.Abstractions.Exceptions;
using AdNoir.Notification.Domain.Emails;
using AdNoir.Notification.Infrastructure.Configuration;
using MongoDB.Driver;

namespace AdNoir.Notification.Infrastructure.Persistence;

/// <summary>Every operation applies the ambient tenant, including worker and consumer operations.</summary>
public sealed class MongoEmailStore(IMongoDatabase database, ITenantContext tenant, IMongoSessionAccessor sessions,
    ICurrentUser currentUser, TimeProvider timeProvider, EmailQueueGuard guard)
    : MongoRepository<EmailDelivery>(database, sessions, tenant, currentUser, timeProvider, collectionName: CollectionName)
{
    /// <summary>Stable collection name.</summary>
    public const string CollectionName = "emailDeliveries";
    /// <summary>Business deduplication is permanent, independent of the finite consumer inbox window.</summary>
    public async Task EnqueueAsync(EmailDelivery delivery, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        if (delivery.TenantId != TenantContext.TenantId)
        {
            throw new InvalidOperationException("Queue tenant mismatch.");
        }

        await guard.EnsureReadyAsync(cancellationToken);
        IClientSessionHandle session = await SessionAccessor.GetSessionAsync(cancellationToken);
        if (!session.IsInTransaction)
        {
            throw new InvalidOperationException("Enqueue requires the consumer outbox transaction.");
        }

        var duplicate = (await BuildScopeFilterAsync(DataScope.Tenant, cancellationToken)) & Filter.Eq(x => x.SourceEventId, delivery.SourceEventId) & Filter.Eq(x => x.Kind, delivery.Kind);
        if (await Collection.Find(session, duplicate).AnyAsync(cancellationToken))
        {
            return;
        }
        // A concurrent duplicate aborts this transaction. Retry sees the committed tombstone.
        await InsertAsync(delivery, cancellationToken);
    }

    /// <summary>Atomically claims one due delivery using its observed version.</summary>
    public async Task<EmailDelivery?> ClaimAsync(DateTime now, int leaseSeconds, bool sendingEnabled, int retentionDays, CancellationToken cancellationToken)
    {
        await guard.EnsureReadyAsync(cancellationToken);
        var due = (await BuildScopeFilterAsync(DataScope.Tenant, cancellationToken)) & Filter.In(x => x.Status, [EmailDeliveryStatus.Pending, EmailDeliveryStatus.RetryScheduled]) &
            Filter.Lte(x => x.NextAttemptAtUtc, now);
        IClientSessionHandle session = await SessionAccessor.GetSessionAsync(cancellationToken);
        EmailDelivery? delivery = await Collection.Find(session, due).SortBy(x => x.NextAttemptAtUtc).FirstOrDefaultAsync(cancellationToken);
        if (delivery is null)
        {
            return null;
        }

        if (!sendingEnabled)
        {
            delivery.Status = EmailDeliveryStatus.Skipped;
            delivery.OutcomeCode = "tenant_disabled";
            delivery.CompletedAtUtc = now;
            delivery.RedactAtUtc = now.AddDays(retentionDays);
            await SaveAsync(delivery, cancellationToken);
            return null;
        }
        delivery.Status = EmailDeliveryStatus.Processing;
        delivery.LeaseId = Guid.CreateVersion7();
        delivery.LeaseUntilUtc = now.AddSeconds(leaseSeconds);
        delivery.Attempts.Add(new EmailAttempt { Number = delivery.Attempts.Count + 1, StartedAtUtc = now });
        return await SaveAsync(delivery, cancellationToken) ? delivery : null;
    }

    /// <summary>CAS update; a stale or expired worker cannot overwrite a newer state.</summary>
    public async Task<bool> SaveAsync(EmailDelivery delivery, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        if (delivery.TenantId != TenantContext.TenantId)
        {
            throw new InvalidOperationException("Queue tenant mismatch.");
        }

        try
        {
            // Workers have no user claims; their explicit tenant scope retains the common tenant and version filters.
            await UpdateWithScopeAsync(delivery, DataScope.Tenant, cancellationToken);
            return true;
        }
        catch (ConcurrencyException)
        {
            // Losing a lease race is an expected worker outcome, not an application exception policy.
            return false;
        }
    }

    /// <summary>Recovers expired work conservatively and removes expired personal content in bounded batches.</summary>
    public async Task MaintainAsync(DateTime now, int retentionDays, CancellationToken cancellationToken)
    {
        var expired = Filter.Eq(x => x.Status, EmailDeliveryStatus.Processing) & Filter.Lte(x => x.LeaseUntilUtc, now);
        var redact = Filter.Eq(x => x.IsRedacted, false) & Filter.Ne(x => x.RedactAtUtc, null) & Filter.Lte(x => x.RedactAtUtc, now);
        IClientSessionHandle session = await SessionAccessor.GetSessionAsync(cancellationToken);
        var rows = await Collection.Find(session, (await BuildScopeFilterAsync(DataScope.Tenant, cancellationToken)) & (expired | redact)).Limit(100).ToListAsync(cancellationToken);
        foreach (EmailDelivery row in rows)
        {
            if (row.Status == EmailDeliveryStatus.Processing)
            {
                row.Status = EmailDeliveryStatus.OutcomeUnknown;
                row.OutcomeCode = "worker_lease_expired";
                row.CompletedAtUtc = now;
                row.RedactAtUtc = now.AddDays(retentionDays);
                row.LeaseId = null;
                row.LeaseUntilUtc = null;
                if (row.Attempts.Count > 0)
                {
                    row.Attempts[^1].Outcome = "worker_lease_expired";
                    row.Attempts[^1].CompletedAtUtc = now;
                }
            }
            if (row.RedactAtUtc <= now)
            {
                row.Recipient = null;
                row.Subject = null;
                row.Body = null;
                row.CorrelationId = null;
                row.IsRedacted = true;
            }
            await SaveAsync(row, cancellationToken);
        }
    }

}

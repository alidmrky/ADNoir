using System.Diagnostics;
using AdNoir.BuildingBlocks.Auditing;
using AdNoir.BuildingBlocks.Context;
using AdNoir.BuildingBlocks.Http;
using AdNoir.BuildingBlocks.Mongo.Sessions;
using AdNoir.Domain.Abstractions.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace AdNoir.BuildingBlocks.Mongo.Auditing;

/// <summary>Persists critical audit records in the same session as business data and outbox.</summary>
public sealed class TransactionalAuditRecorder(IMongoDatabase database, IMongoSessionAccessor sessions,
    ICurrentUser currentUser, ITenantContext tenantContext, IHttpContextAccessor httpContext, TimeProvider clock,
    AuditFieldMasker masker, IOptions<AuditTrailOptions> options) : ITransactionalAuditRecorder
{
    /// <inheritdoc />
    public async Task RecordAsync<TEntity>(Guid tenantId, TEntity entity, ChangeKind kind,
        IReadOnlyList<FieldChange> changes, CancellationToken cancellationToken)
        where TEntity : IEntity, IAuditable
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(changes);
        if (tenantId == Guid.Empty || (entity is IMultiTenant scoped && scoped.TenantId != tenantId))
        {
            throw new InvalidOperationException("Audit tenant must match the target entity.");
        }
        IClientSessionHandle session = await sessions.GetSessionAsync(cancellationToken);
        if (!session.IsInTransaction)
        {
            throw new InvalidOperationException("Critical audit requires an active business transaction.");
        }
        AuditAction action = kind switch
        {
            ChangeKind.Created => AuditAction.Created,
            ChangeKind.Updated => AuditAction.Updated,
            ChangeKind.Deleted => AuditAction.Deleted,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        IReadOnlyList<AuditFieldChange> diff = masker.MaskAll(changes
            .Where(change => change.Before != change.After)
            .Select(change => new AuditFieldChange(change.Field, change.Before, change.After)));
        if (kind == ChangeKind.Updated && diff.Count == 0)
        {
            return;
        }
        AuditLog entry = new()
        {
            TenantId = tenantId,
            UserId = currentUser.IsAuthenticated ? currentUser.UserId : null,
            ActorTenantId = tenantContext.HasTenant ? tenantContext.TenantId : null,
            EntityType = typeof(TEntity).Name,
            EntityId = entity.Id,
            Action = action,
            Changes = diff,
            CorrelationId = httpContext.HttpContext is { } context
                ? ResponseEnricher.ResolveCorrelationId(context) : Activity.Current?.TraceId.ToString(),
            OccurredAtUtc = clock.GetUtcNow().UtcDateTime,
        };
        // IP and user-agent are deliberately omitted: no unbounded or sensitive request data.
        await database.GetCollection<AuditLog>(options.Value.CollectionName)
            .InsertOneAsync(session, entry, options: null, cancellationToken);
    }
}

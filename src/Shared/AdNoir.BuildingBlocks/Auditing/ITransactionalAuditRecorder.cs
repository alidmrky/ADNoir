using AdNoir.Domain.Abstractions.Entities;

namespace AdNoir.BuildingBlocks.Auditing;

/// <summary>Records opt-in changes atomically with the business write; errors propagate.</summary>
public interface ITransactionalAuditRecorder
{
    /// <summary>Records explicitly selected fields inside the caller's active transaction.</summary>
    Task RecordAsync<TEntity>(Guid tenantId, TEntity entity, ChangeKind kind,
        IReadOnlyList<FieldChange> changes, CancellationToken cancellationToken)
        where TEntity : IEntity, IAuditable;
}

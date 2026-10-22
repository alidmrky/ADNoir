namespace AdNoir.Domain.Abstractions.Entities.Bases;

/// <summary>
/// An entity with nothing but an identity.
/// </summary>
/// <remarks>
/// These base classes are a convenience for the common combinations, not a requirement.
/// An entity may implement <see cref="IEntity"/> and its companion interfaces directly and
/// carry exactly the fields it needs; the infrastructure looks at interfaces, not at base
/// types.
/// </remarks>
public abstract class Entity : IEntity
{
    /// <inheritdoc />
    public Guid Id { get; set; } = Guid.CreateVersion7();
}

/// <summary>An entity that records who created it and when.</summary>
public abstract class AuditedEntity : Entity, ICreationAudited, IModificationAudited
{
    /// <inheritdoc />
    public DateTime CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public Guid? CreatedBy { get; set; }

    /// <inheritdoc />
    public DateTime? UpdatedAtUtc { get; set; }

    /// <inheritdoc />
    public Guid? UpdatedBy { get; set; }
}

/// <summary>An audited entity that is soft-deleted and version-checked.</summary>
public abstract class FullAuditedEntity : AuditedEntity, ISoftDelete, IHasVersion
{
    /// <inheritdoc />
    public bool IsDeleted { get; set; }

    /// <inheritdoc />
    public DateTime? DeletedAtUtc { get; set; }

    /// <inheritdoc />
    public Guid? DeletedBy { get; set; }

    /// <inheritdoc />
    public long Version { get; set; }
}

/// <summary>An entity that belongs to a tenant.</summary>
public abstract class TenantEntity : Entity, IMultiTenant
{
    /// <inheritdoc />
    public Guid TenantId { get; set; }
}

/// <summary>The common case: tenant-owned, audited, soft-deleted and version-checked.</summary>
public abstract class FullAuditedTenantEntity : FullAuditedEntity, IMultiTenant
{
    /// <inheritdoc />
    public Guid TenantId { get; set; }
}

/// <summary>A tenant-owned entity that is also a node in the tenant's hierarchy.</summary>
public abstract class HierarchicalTenantEntity : FullAuditedTenantEntity, IHierarchical
{
    /// <inheritdoc />
    public Guid? ParentId { get; set; }

    /// <inheritdoc />
    public string HierarchyPath { get; set; } = "/";

    /// <inheritdoc />
    public IReadOnlyList<Guid> Ancestors { get; set; } = [];
}

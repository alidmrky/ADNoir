namespace AdNoir.Domain.Abstractions.Entities;

/// <summary>
/// Anything persisted with an identity.
/// </summary>
/// <remarks>
/// The infrastructure fills audit, tenant and hierarchy fields by asking what an entity
/// <em>implements</em>, not what it inherits from. That is why these are interfaces and not
/// a base-class chain: an entity carries only the fields it actually needs.
/// </remarks>
public interface IEntity
{
    /// <summary>
    /// Primary key. Generated with <see cref="Guid.CreateVersion7()"/> so that keys are
    /// time-ordered and index-friendly, and stored as a binary GUID in MongoDB rather than
    /// as a string.
    /// </summary>
    Guid Id { get; set; }
}

/// <summary>Records who created the entity and when.</summary>
public interface ICreationAudited
{
    /// <summary>When the entity was created, in UTC.</summary>
    DateTime CreatedAtUtc { get; set; }

    /// <summary>The user who created the entity. Null for records created by the system.</summary>
    Guid? CreatedBy { get; set; }
}

/// <summary>Records who last modified the entity and when.</summary>
public interface IModificationAudited
{
    /// <summary>When the entity was last modified, in UTC. Null while never modified.</summary>
    DateTime? UpdatedAtUtc { get; set; }

    /// <summary>The user who last modified the entity.</summary>
    Guid? UpdatedBy { get; set; }
}

/// <summary>
/// Marks an entity as soft-deletable. The repository filters deleted records out of every
/// query automatically, so a caller cannot read them back by forgetting a condition.
/// </summary>
public interface ISoftDelete
{
    /// <summary>Whether the entity has been deleted.</summary>
    bool IsDeleted { get; set; }

    /// <summary>When the entity was deleted, in UTC.</summary>
    DateTime? DeletedAtUtc { get; set; }

    /// <summary>The user who deleted the entity.</summary>
    Guid? DeletedBy { get; set; }
}

/// <summary>
/// Marks an entity as belonging to a tenant. The repository adds the tenant
/// filter automatically; leaving it out is not possible through the normal API.
/// </summary>
public interface IMultiTenant
{
    /// <summary>The owning tenant.</summary>
    Guid TenantId { get; set; }
}

/// <summary>
/// Marks an entity as taking part in optimistic concurrency.
/// </summary>
/// <remarks>
/// Carrying the field is not enough on its own. The repository puts <see cref="Version"/>
/// into the update <em>filter</em> and increments it in the same operation; if no document
/// matches, another writer got there first and a concurrency exception is raised. A version
/// field that is not part of the filter is decorative.
/// </remarks>
public interface IHasVersion
{
    /// <summary>Row version, incremented on every successful update.</summary>
    long Version { get; set; }
}

/// <summary>
/// Marks an entity as a node in a tenant's hierarchy.
/// </summary>
public interface IHierarchical
{
    /// <summary>The parent node, or null for a root node.</summary>
    Guid? ParentId { get; set; }

    /// <summary>
    /// Human-readable path built from slugs, always starting and ending with <c>/</c>,
    /// e.g. <c>/dealer-1/unit-a/station-5/</c>. Used for display and ordering — never for
    /// subtree queries, because a prefix match would confuse <c>/dealer-1</c> with
    /// <c>/dealer-10</c>.
    /// </summary>
    string HierarchyPath { get; set; }

    /// <summary>
    /// Identifiers of every ancestor of this node. Subtree queries run against this field as
    /// an equality match on a multikey index, which is both faster than a prefix regex and
    /// immune to the slug-prefix collision described above.
    /// </summary>
    IReadOnlyList<Guid> Ancestors { get; set; }
}

/// <summary>
/// Marks an entity as belonging to an organisation unit.
/// </summary>
/// <remarks>
/// Required to translate the <c>Unit</c> and <c>Subtree</c> data scopes into filters.
/// <c>OrgUnitId</c> is the field those scopes compare against, and this interface is how
/// the repository finds it on an arbitrary entity. An entity without it can only be scoped
/// at <c>Tenant</c> or <c>Self</c> level.
/// </remarks>
public interface IOrgUnitScoped
{
    /// <summary>The organisation unit that owns this record.</summary>
    Guid OrgUnitId { get; set; }
}

/// <summary>
/// Opts an entity into the audit trail. Only entities marked with this
/// interface produce <c>AuditLog</c> records; auditing everything would cost writes for data
/// nobody inspects.
/// </summary>
public interface IAuditable;

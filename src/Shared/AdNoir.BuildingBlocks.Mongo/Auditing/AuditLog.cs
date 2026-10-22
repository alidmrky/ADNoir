using AdNoir.Domain.Abstractions.Entities;

namespace AdNoir.BuildingBlocks.Mongo.Auditing;

/// <summary>What happened to the entity.</summary>
public enum AuditAction
{
    /// <summary>The entity was created.</summary>
    Created = 0,

    /// <summary>The entity was modified.</summary>
    Updated = 1,

    /// <summary>The entity was deleted, softly or otherwise.</summary>
    Deleted = 2,
}

/// <summary>One field's before and after values.</summary>
/// <param name="Field">The field name.</param>
/// <param name="Before">The previous value, masked when the field is sensitive.</param>
/// <param name="After">The new value, masked when the field is sensitive.</param>
public sealed record AuditFieldChange(string Field, object? Before, object? After);

/// <summary>
/// An audit trail record. Separate from the audit fields on the entity
/// itself: those say who last touched a row, this says what changed and when.
/// </summary>
/// <remarks>
/// Written only for entities marked <see cref="IAuditable"/>. Auditing everything would cost
/// a write per write for data nobody reads, which is how audit collections end up being the
/// largest thing in the database and the first thing someone disables.
/// </remarks>
public sealed class AuditLog : IEntity, IMultiTenant
{
    /// <inheritdoc />
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <inheritdoc />
    public Guid TenantId { get; set; }

    /// <summary>The user who made the change, or null for system activity.</summary>
    public Guid? UserId { get; set; }

    /// <summary>The actor context tenant; may differ from the target tenant during platform administration.</summary>
    public Guid? ActorTenantId { get; set; }

    /// <summary>The entity's CLR type name.</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>The entity's identifier.</summary>
    public Guid EntityId { get; set; }

    /// <summary>What happened.</summary>
    public AuditAction Action { get; set; }

    /// <summary>The fields that changed, with sensitive values masked.</summary>
    public IReadOnlyList<AuditFieldChange> Changes { get; set; } = [];

    /// <summary>Correlation identifier of the request that caused the change.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>When the change happened, in UTC. Also the TTL anchor.</summary>
    public DateTime OccurredAtUtc { get; set; }

    /// <summary>Caller IP address, when available.</summary>
    public string? IpAddress { get; set; }

    /// <summary>Caller user agent, when available.</summary>
    public string? UserAgent { get; set; }
}

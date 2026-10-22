using AdNoir.Domain.Abstractions.Entities;
using AdNoir.Domain.Abstractions.Entities.Bases;

namespace AdNoir.Sample.Domain.SampleItems;

/// <summary>
/// A demonstration entity used to show the outbox, gRPC and consumer patterns.
/// </summary>
/// <remarks>
/// This entity is intentionally thin. Its two string fields exist so integration tests can
/// create, update and verify data without touching any real business logic.
/// </remarks>
public sealed class SampleItem : FullAuditedTenantEntity
{
    /// <summary>A short title for the item.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>An optional description.</summary>
    public string? Description { get; private set; }

    // Parameterless constructor for MongoDB deserialisation (via BsonClassMap — no [Bson*] here).
    private SampleItem() { }

    /// <summary>Creates a new sample item belonging to the given tenant.</summary>
    /// <param name="tenantId">The tenant that owns the item.</param>
    /// <param name="title">The item title.</param>
    /// <param name="description">An optional description.</param>
    /// <returns>A new, unpersisted <see cref="SampleItem"/>.</returns>
    public static SampleItem Create(Guid tenantId, string title, string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        return new SampleItem
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            Title = title,
            Description = description,
        };
    }

    /// <summary>Updates the item's title and description.</summary>
    /// <param name="title">The new title.</param>
    /// <param name="description">The new description, or <see langword="null"/> to clear it.</param>
    public void Update(string title, string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        Title = title;
        Description = description;
    }
}

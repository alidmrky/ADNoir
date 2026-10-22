using AdNoir.Domain.Abstractions.Entities;
using MongoDB.Driver;

namespace AdNoir.BuildingBlocks.Mongo.Indexing;

/// <summary>
/// Supplies index definitions for a collection. Implementations are collected and executed by
/// <c>AdNoir.DbMigrator</c>.
/// </summary>
/// <remarks>
/// Indexes are not created at application startup. Startup index creation races between
/// instances, delays readiness, and quietly needs write privileges the application should
/// not have in production. The migrator runs once, deliberately.
/// </remarks>
public interface IMongoIndexProvider
{
    /// <summary>Creates or updates this collection's indexes.</summary>
    /// <param name="database">The database.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes once the indexes exist.</returns>
    Task EnsureIndexesAsync(IMongoDatabase database, CancellationToken cancellationToken = default);
}

/// <summary>Ready-made index definitions for the recurring shapes.</summary>
public static class MongoIndexHelpers
{
    /// <summary>
    /// <c>{ TenantId: 1, Ancestors: 1 }</c> - the index subtree queries rely on.
    /// </summary>
    /// <typeparam name="TEntity">A tenant-owned hierarchy node.</typeparam>
    /// <returns>The index model.</returns>
    public static CreateIndexModel<TEntity> TenantAncestors<TEntity>()
        where TEntity : IMultiTenant, IHierarchical =>
        new(
            Builders<TEntity>.IndexKeys
                .Ascending(new StringFieldDefinition<TEntity, Guid>(nameof(IMultiTenant.TenantId)))
                .Ascending(new StringFieldDefinition<TEntity, Guid>(nameof(IHierarchical.Ancestors))),
            new CreateIndexOptions { Name = "ix_tenant_ancestors" });

    /// <summary>
    /// <c>{ TenantId: 1, HierarchyPath: 1 }</c> - for display ordering and path lookups.
    /// </summary>
    /// <typeparam name="TEntity">A tenant-owned hierarchy node.</typeparam>
    /// <returns>The index model.</returns>
    /// <remarks>
    /// Any regex used against <c>HierarchyPath</c> must be anchored with <c>^</c> and
    /// case-sensitive. The <c>i</c> option prevents the index from being used at all, turning
    /// what looks like an indexed lookup into a collection scan.
    /// </remarks>
    public static CreateIndexModel<TEntity> TenantHierarchyPath<TEntity>()
        where TEntity : IMultiTenant, IHierarchical =>
        new(
            Builders<TEntity>.IndexKeys
                .Ascending(new StringFieldDefinition<TEntity, Guid>(nameof(IMultiTenant.TenantId)))
                .Ascending(new StringFieldDefinition<TEntity, string>(nameof(IHierarchical.HierarchyPath))),
            new CreateIndexOptions { Name = "ix_tenant_hierarchy_path" });

    /// <summary>
    /// <c>{ TenantId: 1 }</c> - the baseline index for a tenant-owned collection.
    /// </summary>
    /// <typeparam name="TEntity">A tenant-owned entity.</typeparam>
    /// <returns>The index model.</returns>
    public static CreateIndexModel<TEntity> Tenant<TEntity>()
        where TEntity : IMultiTenant =>
        new(
            Builders<TEntity>.IndexKeys
                .Ascending(new StringFieldDefinition<TEntity, Guid>(nameof(IMultiTenant.TenantId))),
            new CreateIndexOptions { Name = "ix_tenant" });

    /// <summary>Creates a TTL index that expires documents after a period.</summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="dateFieldName">The <c>DateTime</c> field the expiry is measured from.</param>
    /// <param name="retention">How long documents are kept.</param>
    /// <param name="name">Index name.</param>
    /// <returns>The index model.</returns>
    public static CreateIndexModel<TEntity> Ttl<TEntity>(
        string dateFieldName,
        TimeSpan retention,
        string name = "ix_ttl")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dateFieldName);

        return new CreateIndexModel<TEntity>(
            Builders<TEntity>.IndexKeys.Ascending(new StringFieldDefinition<TEntity, DateTime>(dateFieldName)),
            new CreateIndexOptions { Name = name, ExpireAfter = retention });
    }

    /// <summary>
    /// Creates a unique index scoped to a tenant, so the same value may repeat across tenants
    /// but not within one.
    /// </summary>
    /// <typeparam name="TEntity">A tenant-owned entity.</typeparam>
    /// <param name="fieldName">The field that must be unique within the tenant.</param>
    /// <param name="name">Index name.</param>
    /// <param name="softDeleteAware">
    /// When true the index only covers records that are not soft-deleted, so deleting a record
    /// frees its value for reuse. Without this a deleted user's e-mail address stays taken
    /// forever, which reads as a bug to everyone who hits it.
    /// </param>
    /// <returns>The index model.</returns>
    public static CreateIndexModel<TEntity> UniquePerTenant<TEntity>(
        string fieldName,
        string name,
        bool softDeleteAware = true)
        where TEntity : IMultiTenant
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        IndexKeysDefinition<TEntity> keys = Builders<TEntity>.IndexKeys
            .Ascending(new StringFieldDefinition<TEntity, Guid>(nameof(IMultiTenant.TenantId)))
            .Ascending(new StringFieldDefinition<TEntity, object>(fieldName));

        CreateIndexOptions<TEntity> options = new() { Name = name, Unique = true };

        if (softDeleteAware && typeof(ISoftDelete).IsAssignableFrom(typeof(TEntity)))
        {
            options.PartialFilterExpression = Builders<TEntity>.Filter.Eq(
                new StringFieldDefinition<TEntity, bool>(nameof(ISoftDelete.IsDeleted)),
                false);
        }

        return new CreateIndexModel<TEntity>(keys, options);
    }
}

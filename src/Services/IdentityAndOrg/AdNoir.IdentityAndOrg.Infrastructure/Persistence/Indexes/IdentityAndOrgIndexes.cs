using AdNoir.BuildingBlocks.Mongo.Auditing;
using AdNoir.BuildingBlocks.Mongo.Indexing;
using AdNoir.BuildingBlocks.Mongo.Repositories;
using AdNoir.IdentityAndOrg.Domain.Identity;
using AdNoir.IdentityAndOrg.Domain.OrgUnits;
using AdNoir.IdentityAndOrg.Domain.Tenants;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace AdNoir.IdentityAndOrg.Infrastructure.Persistence.Indexes;

/// <summary>Indexes for the tenant collection.</summary>
public sealed class TenantIndexProvider : IMongoIndexProvider
{
    /// <inheritdoc />
    public async Task EnsureIndexesAsync(IMongoDatabase database, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);

        IMongoCollection<Tenant> collection =
            database.GetCollection<Tenant>(MongoCollectionNaming.For(typeof(Tenant)));

        // Partial on IsDeleted so a deleted tenant releases its slug. Without that, a slug is
        // taken forever by a record nobody can see, which reads as a bug to whoever hits it.
        CreateIndexModel<Tenant> uniqueSlug = new(
            Builders<Tenant>.IndexKeys.Ascending(tenant => tenant.Slug),
            new CreateIndexOptions<Tenant>
            {
                Name = "ux_tenant_slug",
                Unique = true,
                PartialFilterExpression = Builders<Tenant>.Filter.Eq(tenant => tenant.IsDeleted, false),
            });

        CreateIndexModel<Tenant> status = new(
            Builders<Tenant>.IndexKeys.Ascending(tenant => tenant.Status),
            new CreateIndexOptions { Name = "ix_tenant_status" });

        await collection.Indexes.CreateManyAsync([uniqueSlug, status], cancellationToken);
    }
}

/// <summary>Indexes for the organisation unit collection.</summary>
public sealed class OrgUnitIndexProvider : IMongoIndexProvider
{
    /// <inheritdoc />
    public async Task EnsureIndexesAsync(IMongoDatabase database, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);

        IMongoCollection<OrgUnit> collection =
            database.GetCollection<OrgUnit>(MongoCollectionNaming.For(typeof(OrgUnit)));

        // The two indexes the hierarchy depends on. The first one carries every subtree
        // query; without it those become collection scans that still return correct results,
        // which is why the omission would go unnoticed until the tree grows.
        CreateIndexModel<OrgUnit> tenantAncestors = MongoIndexHelpers.TenantAncestors<OrgUnit>();
        CreateIndexModel<OrgUnit> tenantPath = MongoIndexHelpers.TenantHierarchyPath<OrgUnit>();

        // Sibling slugs are unique within a parent, not globally.
        CreateIndexModel<OrgUnit> uniqueSiblingSlug = new(
            Builders<OrgUnit>.IndexKeys
                .Ascending(unit => unit.TenantId)
                .Ascending(unit => unit.ParentId)
                .Ascending(unit => unit.Slug),
            new CreateIndexOptions<OrgUnit>
            {
                Name = "ux_orgunit_parent_slug",
                Unique = true,
                PartialFilterExpression = Builders<OrgUnit>.Filter.Eq(unit => unit.IsDeleted, false),
            });

        CreateIndexModel<OrgUnit> tenantParent = new(
            Builders<OrgUnit>.IndexKeys
                .Ascending(unit => unit.TenantId)
                .Ascending(unit => unit.ParentId),
            new CreateIndexOptions { Name = "ix_orgunit_tenant_parent" });

        await collection.Indexes.CreateManyAsync(
            [tenantAncestors, tenantPath, uniqueSiblingSlug, tenantParent],
            cancellationToken);
    }
}

/// <summary>Indexes for the user collection.</summary>
public sealed class UserIndexProvider : IMongoIndexProvider
{
    /// <inheritdoc />
    public async Task EnsureIndexesAsync(IMongoDatabase database, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);

        IMongoCollection<User> collection =
            database.GetCollection<User>(MongoCollectionNaming.For(typeof(User)));

        // Unique per tenant, not globally: the same person may hold accounts in two tenants,
        // which is why sign-in asks which tenant to sign in to.
        CreateIndexModel<User> uniqueEmail = new(
            Builders<User>.IndexKeys
                .Ascending(user => user.TenantId)
                .Ascending(user => user.NormalizedEmail),
            new CreateIndexOptions<User>
            {
                Name = "ux_user_tenant_email",
                Unique = true,
                PartialFilterExpression = Builders<User>.Filter.Eq(user => user.IsDeleted, false),
            });

        CreateIndexModel<User> tenantOrgUnit = new(
            Builders<User>.IndexKeys
                .Ascending(user => user.TenantId)
                .Ascending(user => user.OrgUnitId),
            new CreateIndexOptions { Name = "ix_user_tenant_orgunit" });

        await collection.Indexes.CreateManyAsync([uniqueEmail, tenantOrgUnit], cancellationToken);
    }
}

/// <summary>Indexes for the role collection.</summary>
public sealed class RoleIndexProvider : IMongoIndexProvider
{
    /// <inheritdoc />
    public async Task EnsureIndexesAsync(IMongoDatabase database, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);

        IMongoCollection<Role> collection =
            database.GetCollection<Role>(MongoCollectionNaming.For(typeof(Role)));

        // TenantId is null for system roles, and MongoDB treats null as a value here, so the
        // pair stays unique for both global and tenant-owned roles.
        CreateIndexModel<Role> uniqueCode = new(
            Builders<Role>.IndexKeys
                .Ascending(role => role.TenantId)
                .Ascending(role => role.Code),
            new CreateIndexOptions<Role>
            {
                Name = "ux_role_tenant_code",
                Unique = true,
                PartialFilterExpression = Builders<Role>.Filter.Eq(role => role.IsDeleted, false),
            });

        await collection.Indexes.CreateManyAsync([uniqueCode], cancellationToken);
    }
}

/// <summary>Indexes for the refresh token collection.</summary>
public sealed class RefreshTokenIndexProvider : IMongoIndexProvider
{
    /// <inheritdoc />
    public async Task EnsureIndexesAsync(IMongoDatabase database, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);

        IMongoCollection<RefreshToken> collection =
            database.GetCollection<RefreshToken>(MongoCollectionNaming.For(typeof(RefreshToken)));

        // The hash is how a presented token is found, on every refresh. Unique because two
        // records with the same hash would make reuse detection ambiguous.
        CreateIndexModel<RefreshToken> uniqueHash = new(
            Builders<RefreshToken>.IndexKeys.Ascending(token => token.TokenHash),
            new CreateIndexOptions { Name = "ux_refresh_token_hash", Unique = true });

        // Revoking a whole family on reuse detection has to be one indexed operation.
        CreateIndexModel<RefreshToken> family = new(
            Builders<RefreshToken>.IndexKeys
                .Ascending(token => token.FamilyId)
                .Ascending(token => token.SequenceNumber),
            new CreateIndexOptions { Name = "ix_refresh_token_family" });

        CreateIndexModel<RefreshToken> user = new(
            Builders<RefreshToken>.IndexKeys.Ascending(token => token.UserId),
            new CreateIndexOptions { Name = "ix_refresh_token_user" });

        // Expired records are removed by the database, so the collection does not grow without
        // bound if a cleanup job is never written.
        CreateIndexModel<RefreshToken> ttl = MongoIndexHelpers.Ttl<RefreshToken>(
            nameof(RefreshToken.ExpiresAtUtc),
            TimeSpan.Zero,
            "ix_refresh_token_ttl");

        await collection.Indexes.CreateManyAsync([uniqueHash, family, user, ttl], cancellationToken);
    }
}

/// <summary>Indexes for the audit trail collection.</summary>
public sealed class AuditLogIndexProvider(IOptions<AuditTrailOptions> options) : IMongoIndexProvider
{
    private readonly AuditTrailOptions _options = options.Value;

    /// <inheritdoc />
    public async Task EnsureIndexesAsync(IMongoDatabase database, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);

        IMongoCollection<AuditLog> collection =
            database.GetCollection<AuditLog>(_options.CollectionName);

        CreateIndexModel<AuditLog> lookup = new(
            Builders<AuditLog>.IndexKeys
                .Ascending(log => log.TenantId)
                .Ascending(log => log.EntityType)
                .Ascending(log => log.EntityId)
                .Descending(log => log.OccurredAtUtc),
            new CreateIndexOptions { Name = "ix_audit_entity" });

        // Retention is enforced by the database rather than by a cleanup job, so it keeps
        // working even if the job is never written.
        CreateIndexModel<AuditLog> ttl = MongoIndexHelpers.Ttl<AuditLog>(
            nameof(AuditLog.OccurredAtUtc),
            _options.Retention,
            "ix_audit_ttl");

        await collection.Indexes.CreateManyAsync([lookup, ttl], cancellationToken);
    }
}

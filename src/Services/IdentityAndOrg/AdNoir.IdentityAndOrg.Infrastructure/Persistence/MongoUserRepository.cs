using AdNoir.BuildingBlocks.Context;
using AdNoir.BuildingBlocks.Mongo.Repositories;
using AdNoir.BuildingBlocks.Mongo.Sessions;
using AdNoir.Domain.Abstractions.Repositories;
using AdNoir.IdentityAndOrg.Domain.Identity;
using MongoDB.Driver;

namespace AdNoir.IdentityAndOrg.Infrastructure.Persistence;

/// <summary>MongoDB implementation of <see cref="IUserRepository"/>.</summary>
public sealed class MongoUserRepository(
    IMongoDatabase database,
    IMongoSessionAccessor sessionAccessor,
    ITenantContext tenantContext,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IOrgUnitSubtreeResolver? subtreeResolver = null)
    : MongoRepository<User>(database, sessionAccessor, tenantContext, currentUser, timeProvider, subtreeResolver),
      IUserRepository
{
    /// <inheritdoc />
    public Task<User?> FindByIdForAuthenticationAsync(Guid id, CancellationToken cancellationToken = default) =>
        FindWithinTenantAsync(user => user.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task UpdateForAuthenticationAsync(User user, CancellationToken cancellationToken = default) =>
        UpdateWithScopeAsync(user, DataScope.Tenant, cancellationToken);

    /// <inheritdoc />
    public Task<User?> FindByNormalizedEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedEmail);

        // Tenant-wide on purpose. Sign-in happens before any role is known, so narrowing by
        // the caller's data scope here would make it depend on who is signing in.
        return FindWithinTenantAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> EmailExistsAsync(
        string normalizedEmail,
        Guid? excludingId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedEmail);

        FilterDefinition<User> filter = Filter.And(
            await BuildScopeFilterAsync(DataScope.Tenant, cancellationToken),
            Filter.Eq(user => user.NormalizedEmail, normalizedEmail));

        if (excludingId is Guid excluded)
        {
            filter = Filter.And(filter, Filter.Ne(user => user.Id, excluded));
        }

        IClientSessionHandle session = await SessionAccessor.GetSessionAsync(cancellationToken);

        return await Collection.Find(session, filter).AnyAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<User>> ListByRoleAsync(
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        FilterDefinition<User> filter = Filter.And(
            await BuildScopeFilterAsync(DataScope.Tenant, cancellationToken),
            Filter.ElemMatch(user => user.RoleAssignments, assignment => assignment.RoleId == roleId));

        IClientSessionHandle session = await SessionAccessor.GetSessionAsync(cancellationToken);

        return await Collection.Find(session, filter).ToListAsync(cancellationToken);
    }

    private async Task<User?> FindWithinTenantAsync(
        System.Linq.Expressions.Expression<Func<User, bool>> predicate,
        CancellationToken cancellationToken)
    {
        FilterDefinition<User> filter = Filter.And(
            await BuildScopeFilterAsync(DataScope.Tenant, cancellationToken),
            Filter.Where(predicate));

        IClientSessionHandle session = await SessionAccessor.GetSessionAsync(cancellationToken);

        return await Collection.Find(session, filter).FirstOrDefaultAsync(cancellationToken);
    }
}

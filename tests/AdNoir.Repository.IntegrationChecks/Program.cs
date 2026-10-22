using AdNoir.BuildingBlocks.Auditing;
using AdNoir.BuildingBlocks.Mongo.Auditing;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using AdNoir.BuildingBlocks.Context;
using AdNoir.BuildingBlocks.Mongo.Conventions;
using AdNoir.BuildingBlocks.Mongo.Repositories;
using AdNoir.BuildingBlocks.Mongo.Sessions;
using AdNoir.Domain.Abstractions.Exceptions;
using AdNoir.Domain.Abstractions.Repositories;
using AdNoir.IdentityAndOrg.Domain.Identity;
using AdNoir.IdentityAndOrg.Domain.OrgUnits;
using AdNoir.IdentityAndOrg.Infrastructure.Persistence;
using AdNoir.Sample.Domain.SampleItems;
using AdNoir.Sample.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using MongoDB.Driver;

string databaseName = "adnoir_repository_check_" + Guid.NewGuid().ToString("N");
var commands = new System.Collections.Concurrent.ConcurrentQueue<(string Name, bool HasSession, long Limit)>();
MongoClientSettings settings = MongoClientSettings.FromConnectionString("mongodb://localhost:27017/?replicaSet=rs0");
settings.ClusterConfigurator = cluster => cluster.Subscribe<MongoDB.Driver.Core.Events.CommandStartedEvent>(command =>
{
    if (command.DatabaseNamespace.DatabaseName == databaseName)
    {
        commands.Enqueue((command.CommandName, command.Command.Contains("lsid"),
            command.Command.GetValue("limit", 0).ToInt64()));
    }
});
MongoClient client = new(settings);
IMongoDatabase database = client.GetDatabase(databaseName);
AdNoirMongoConventions.Register();
IdentityAndOrgClassMaps.Register();
SampleClassMaps.Register();
// These checks exercise repository transactions only; they do not claim to test the outbox.
#pragma warning disable CS0618
await using StandaloneMongoSessionAccessor sessions = new(client);
#pragma warning restore CS0618
IClientSessionHandle session = await sessions.GetSessionAsync();
Guid tenantA = Guid.CreateVersion7();
Guid tenantB = Guid.CreateVersion7();
Guid userId = Guid.CreateVersion7();
TenantContext tenant = new();
tenant.SetTenant(tenantA, true);
HttpContextAccessor http = new() { HttpContext = new DefaultHttpContext() };
HttpContextCurrentUser user = new(http);
OrgUnit root = OrgUnit.CreateRoot(tenantA, "A", "a");
OrgUnit child = OrgUnit.CreateChild(root, "Child", "child");
OrgUnit foreign = OrgUnit.CreateRoot(tenantB, "B", "b");
OrgUnit deleted = OrgUnit.CreateChild(root, "Deleted", "deleted");
deleted.IsDeleted = true;
foreach (OrgUnit row in new[] { root, child, foreign, deleted }) { row.Version = 1; }
IMongoCollection<OrgUnit> units = database.GetCollection<OrgUnit>(MongoCollectionNaming.For(typeof(OrgUnit)));
IMongoCollection<RefreshToken> tokens = database.GetCollection<RefreshToken>(MongoCollectionNaming.For(typeof(RefreshToken)));
OrgUnitSubtreeResolver resolver = new(database, tenant, sessions);
MongoOrgUnitRepository orgs = new(database, sessions, tenant, user, TimeProvider.System, resolver);
MongoRefreshTokenRepository refresh = new(database, sessions, tenant, user, TimeProvider.System);
int passed = 0;
SetScope(DataScope.Tenant);
try
{
    await units.InsertManyAsync([root, child, foreign, deleted]);
    commands.Clear();
    var limited = await orgs.ListLimitedAsync(10, orderBy: row => row.Version, descending: true);
    var observed = commands.ToArray();
    Check(limited.Count == 2 && limited.All(row => row.TenantId == tenantA && !row.IsDeleted),
        "limited read preserves tenant and soft-delete filters");
    Check(observed.Length == 1 && observed[0] == ("find", true, 10L),
        "limited read sends one bounded find with shared session and no count");
    var paged = await orgs.ListPagedAsync(1, 10, orderBy: row => row.Version, descending: true);
    Check(limited.Select(row => row.Id).SequenceEqual(paged.Items.Select(row => row.Id)) && paged.TotalCount == 2,
        "limited read preserves paged tie-breaker and paged total remains accurate");
    Check((await orgs.ListLimitedAsync(1)).Count == 1, "limited read enforces requested bound");
    Check((await orgs.ListLimitedAsync(10, row => row.Id == foreign.Id)).Count == 0,
        "limited predicate cannot expose a foreign tenant");
    await ExpectRejectedAsync<ArgumentOutOfRangeException>(() => orgs.ListLimitedAsync(0), "zero limit rejected");
    await ExpectRejectedAsync<ArgumentOutOfRangeException>(() => orgs.ListLimitedAsync(-1), "negative limit rejected");
    using (var cancelled = new CancellationTokenSource())
    {
        cancelled.Cancel();
        await ExpectRejectedAsync<OperationCanceledException>(() => orgs.ListLimitedAsync(1, cancellationToken: cancelled.Token),
            "limited read observes cancellation");
    }
    SetScope(DataScope.Tenant, DataScope.Unit);
    var unitRows = await orgs.ListLimitedAsync(10);
    Check(unitRows.Count == 1 && unitRows[0].Id == root.Id, "limited read preserves narrow scope");
    SetScope(DataScope.Tenant);
    session.StartTransaction();
    OrgUnit transient = OrgUnit.CreateChild(root, "Transient", "transient");
    await units.InsertOneAsync(session, transient);
    Check((await orgs.ListLimitedAsync(10, row => row.Id == transient.Id)).Count == 1,
        "limited read shares the active transaction");
    await session.AbortTransactionAsync();
    Check((await orgs.ListLimitedAsync(10, row => row.Id == transient.Id)).Count == 0,
        "limited read no longer sees rolled-back data");

    await ExpectRejectedAsync<InvalidOperationException>(() => orgs.ApplySubtreeRewritesAsync([Rewrite(child)]),
        "bulk rewrite requires a transaction");
    await RejectedRewriteAsync([Rewrite(child), Rewrite(foreign)], "cross-tenant bulk rejected and rolled back");
    await RejectedRewriteAsync([Rewrite(deleted)], "soft-deleted node excluded from bulk");
    await RejectedRewriteAsync([Rewrite(child) with { ExpectedVersion = 99 }], "stale bulk version rejected");
    SetScope(DataScope.Unit);
    await RejectedRewriteAsync([Rewrite(child)], "write scope limits bulk despite tenant read scope");
    SetScope(DataScope.Tenant);
    session.StartTransaction();
    Check(await orgs.ApplySubtreeRewritesAsync([Rewrite(child)]) == 1, "valid subtree rewrite succeeds");
    await session.CommitTransactionAsync();
    OrgUnit updated = await units.Find(row => row.Id == child.Id).SingleAsync();
    Check(updated.Version == 2 && updated.HierarchyPath == "/rewritten", "bulk increments expected version");

    session.StartTransaction();
    OrgUnit pending = OrgUnit.CreateChild(root, "Pending", "pending");
    await orgs.InsertAsync(pending);
    IReadOnlyList<Guid> subtree = await resolver.GetSubtreeIdsAsync(root.Id);
    Check(subtree.Contains(pending.Id) && !subtree.Contains(deleted.Id) && !subtree.Contains(foreign.Id),
        "subtree resolver shares transaction and tenant filters");
    await session.AbortTransactionAsync();
    Check(await units.CountDocumentsAsync(row => row.Id == pending.Id) == 0, "uncommitted subtree insert rolled back");

    Guid family = Guid.CreateVersion7();
    RefreshToken a = Token(tenantA, family);
    RefreshToken b = Token(tenantB, family);
    RefreshToken aOther = Token(tenantA, Guid.CreateVersion7());
    RefreshToken bOther = Token(tenantB, Guid.CreateVersion7());
    await tokens.InsertManyAsync([a, b, aOther, bOther]);
    Check(await refresh.RevokeFamilyAsync(family, RefreshTokenRevocationReason.SignedOut, DateTime.UtcNow) == 1,
        "family revoke limited to ambient tenant");
    Check((await tokens.Find(row => row.Id == b.Id).SingleAsync()).RevokedAtUtc is null,
        "foreign tenant family remains usable");
    Check(await refresh.RevokeAllForUserAsync(userId, RefreshTokenRevocationReason.SignedOut, DateTime.UtcNow) == 1,
        "user revoke limited to ambient tenant");
    Check((await tokens.Find(row => row.Id == bOther.Id).SingleAsync()).RevokedAtUtc is null,
        "foreign tenant user tokens remain usable");
    MongoRefreshTokenRepository unresolved = new(database, sessions, new TenantContext(), user, TimeProvider.System);
    Check((await unresolved.FindByHashAsync(a.TokenHash))?.Id == a.Id,
        "explicit cross-tenant bootstrap still finds revoked tokens");
    await ExpectRejectedAsync<InvalidOperationException>(
        () => unresolved.RevokeFamilyAsync(family, RefreshTokenRevocationReason.SignedOut, DateTime.UtcNow),
        "revoke without tenant fails closed");

    MongoSampleItemRepository sample = new(database, sessions, tenant, user, TimeProvider.System);
    SampleItem item = SampleItem.Create(tenantA, "Example", null);
    await sample.InsertAsync(item);
    Check((await sample.FindByIdAsync(item.Id))?.Title == "Example", "Sample uses shared insert and read");
    tenant.SetTenant(tenantB, true);
    Check(await sample.FindByIdAsync(item.Id) is null, "Sample hides foreign tenant item");
    tenant.SetTenant(tenantA, true);
    SampleItem stale = (await sample.FindByIdAsync(item.Id))!;
    item.Update("Updated", null);
    await sample.UpdateAsync(item);
    await ExpectRejectedAsync<ConcurrencyException>(() => sample.UpdateAsync(stale), "Sample shared version check rejects stale write");
    await sample.DeleteAsync(item);
    Check(await sample.FindByIdAsync(item.Id) is null, "Sample shared soft-delete hides item");
    var auditOptions = Options.Create(new AuditTrailOptions { SensitiveFieldNames = [] });
    TransactionalAuditRecorder audit = new(database, sessions, user, tenant, http, TimeProvider.System,
        new AuditFieldMasker(auditOptions), auditOptions);
    await ExpectRejectedAsync<InvalidOperationException>(() => audit.RecordAsync(tenantA, root, ChangeKind.Updated,
        [new FieldChange("Password", "old-value", "new-value")], CancellationToken.None), "critical audit requires transaction");
    session.StartTransaction();
    await ExpectRejectedAsync<InvalidOperationException>(() => audit.RecordAsync(tenantB, root, ChangeKind.Created,
        [], CancellationToken.None), "critical audit rejects target tenant mismatch");
    await audit.RecordAsync(tenantA, root, ChangeKind.Updated,
        [new FieldChange("PasswordHash", "old-value", "new-value")], CancellationToken.None);
    IMongoCollection<AuditLog> auditLogs = database.GetCollection<AuditLog>(auditOptions.Value.CollectionName);
    AuditLog persisted = await auditLogs.Find(session, row => row.EntityId == root.Id).SingleAsync();
    Check((string?)persisted.Changes[0].Before == "***" && (string?)persisted.Changes[0].After == "***",
        "persisted audit keeps mandatory masks with empty configured list");
    await session.AbortTransactionAsync();
    Check(await auditLogs.CountDocumentsAsync(row => row.EntityId == root.Id) == 0,
        "critical audit itself rolls back with transaction");
    Console.WriteLine($"PASS {passed} repository integration checks. No unit tests.");
}
finally
{
    if (session.IsInTransaction) { await session.AbortTransactionAsync(); }
    await CleanupAsync(client, databaseName);
    Console.WriteLine("Temporary repository database removed.");
}

void SetScope(DataScope writeScope, DataScope readScope = DataScope.Tenant) => http.HttpContext!.User = new ClaimsPrincipal(new ClaimsIdentity([
    new Claim(AdNoirClaimTypes.Subject, userId.ToString()),
    new Claim(AdNoirClaimTypes.OrgUnitId, root.Id.ToString()),
    new Claim(AdNoirClaimTypes.ReadScope, readScope.ToString()),
    new Claim(AdNoirClaimTypes.WriteScope, writeScope.ToString())], "integration"));
OrgUnitRewrite Rewrite(OrgUnit row) => new(row.Id, "/rewritten", row.Ancestors, row.Version);
RefreshToken Token(Guid tenantId, Guid familyId)
{
    RefreshToken token = RefreshToken.StartFamily(tenantId, userId,
        RefreshTokenGenerator.Hash(RefreshTokenGenerator.NewToken()), DateTime.UtcNow, TimeSpan.FromDays(1), null);
    token.FamilyId = familyId;
    return token;
}
async Task RejectedRewriteAsync(IReadOnlyList<OrgUnitRewrite> rewrites, string label)
{
    session.StartTransaction();
    try { await ExpectRejectedAsync<ConcurrencyException>(() => orgs.ApplySubtreeRewritesAsync(rewrites), label); }
    finally { await session.AbortTransactionAsync(); }
    OrgUnit unchanged = await units.Find(row => row.Id == child.Id).SingleAsync();
    Check(unchanged.Version == 1 && unchanged.HierarchyPath == child.HierarchyPath, "failed rewrite leaves child unchanged");
}
async Task ExpectRejectedAsync<TException>(Func<Task> action, string label) where TException : Exception
{
    bool rejected = false;
    try { await action(); }
    catch (TException) { rejected = true; }
    Check(rejected, label);
}
void Check(bool condition, string label)
{
    if (!condition) { throw new InvalidOperationException(label); }
    passed++;
    Console.WriteLine("PASS " + label);
}

static Task CleanupAsync(IMongoClient client, string name)
{
    if (!name.StartsWith("adnoir_repository_check_", StringComparison.Ordinal) || name.Length != 56)
    {
        throw new InvalidOperationException("Unexpected cleanup target.");
    }
    return client.DropDatabaseAsync(name);
}

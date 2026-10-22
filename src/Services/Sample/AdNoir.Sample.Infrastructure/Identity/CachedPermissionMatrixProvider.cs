using AdNoir.Domain.Abstractions.Exceptions;
using AdNoir.Shared.Contracts.Messages;
using AdNoir.BuildingBlocks.Authorization;
using Microsoft.Extensions.Caching.Hybrid;

namespace AdNoir.Sample.Infrastructure.Identity;

/// <summary>Caches serializable permission snapshots by tenant and version.</summary>
public sealed class CachedPermissionMatrixProvider(IPermissionMatrixProvider inner, HybridCache cache) : IPermissionMatrixProvider
{
    private static readonly HybridCacheEntryOptions EntryOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(5),
        LocalCacheExpiration = TimeSpan.FromSeconds(30),
    };

    /// <inheritdoc />
    public async Task<PermissionMatrix> GetAsync(Guid tenantId, long version, CancellationToken cancellationToken = default)
    {
        string key = FormattableString.Invariant($"sample:permissions:v1:{tenantId:N}:{version}");
        PermissionSnapshot snapshot = await cache.GetOrCreateAsync(
            key,
            async token =>
            {
                PermissionMatrix matrix = await inner.GetAsync(tenantId, version, token);
                return new PermissionSnapshot(matrix.TenantId, matrix.Version,
                    matrix.Roles.ToDictionary(role => role, role => matrix.PermissionsOf(role).ToArray(), StringComparer.OrdinalIgnoreCase));
            },
            options: EntryOptions,
            cancellationToken: cancellationToken);

        if (snapshot.TenantId != tenantId || snapshot.Version != version)
        {
            throw new ExternalServiceException(MessageCodes.Auth.PermissionMatrixUnavailable, "identity",
                "Cached permission matrix does not match the requested tenant and version.");
        }

        return new PermissionMatrix(snapshot.TenantId, snapshot.Version,
            snapshot.Roles.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<string>)pair.Value, StringComparer.OrdinalIgnoreCase));
    }
}

/// <summary>JSON-compatible cache payload; frozen collections stay outside the distributed cache.</summary>
/// <param name="TenantId">Tenant owning the matrix.</param>
/// <param name="Version">Permission version.</param>
/// <param name="Roles">Permissions indexed by role code.</param>
public sealed record PermissionSnapshot(Guid TenantId, long Version, Dictionary<string, string[]> Roles);


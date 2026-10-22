using AdNoir.BuildingBlocks.Authorization;
using AdNoir.Domain.Abstractions.Exceptions;
using AdNoir.IdentityAndOrg.Domain.Identity;
using AdNoir.IdentityAndOrg.Domain.Tenants;
using AdNoir.Shared.Contracts.Messages;

namespace AdNoir.IdentityAndOrg.Infrastructure.Security;

/// <summary>Loads Identity's own permission matrix without making a network call to itself.</summary>
public sealed class LocalPermissionMatrixProvider(ITenantRepository tenants, IRoleRepository roles) : IPermissionMatrixProvider
{
    /// <inheritdoc />
    public async Task<PermissionMatrix> GetAsync(Guid tenantId, long version, CancellationToken cancellationToken = default)
    {
        Tenant? tenant = await tenants.FindByIdAsync(tenantId, cancellationToken);
        if (tenant is null || !tenant.IsUsable || tenant.PermissionVersion != version)
        {
            throw Unavailable();
        }

        IReadOnlyList<Role> entries = await roles.ListForTenantAsync(tenantId, cancellationToken);
        Dictionary<string, IReadOnlyList<string>> permissions = new(StringComparer.OrdinalIgnoreCase);
        foreach (Role role in entries)
        {
            if (!permissions.TryAdd(role.Code, role.Permissions.ToArray()))
            {
                throw Unavailable();
            }
        }

        // Reject concurrent permission changes rather than returning a mixed-version snapshot.
        Tenant? current = await tenants.FindByIdAsync(tenantId, cancellationToken);
        if (current is null || !current.IsUsable || current.PermissionVersion != version)
        {
            throw Unavailable();
        }

        return new PermissionMatrix(tenantId, version, permissions);
    }

    private static ExternalServiceException Unavailable() =>
        new(MessageCodes.Auth.PermissionMatrixUnavailable, "identity", "The requested tenant permission version is unavailable.");
}

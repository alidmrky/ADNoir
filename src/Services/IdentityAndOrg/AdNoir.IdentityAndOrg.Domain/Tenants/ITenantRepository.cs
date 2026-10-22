using AdNoir.Domain.Abstractions.Repositories;

namespace AdNoir.IdentityAndOrg.Domain.Tenants;

/// <summary>
/// Tenant persistence. Declared in Domain, implemented in Infrastructure.
/// </summary>
public interface ITenantRepository : IRepository<Tenant>
{
    /// <summary>Finds a tenant by its slug.</summary>
    /// <param name="slug">The slug.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The tenant, or null.</returns>
    Task<Tenant?> FindBySlugAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>Whether a slug is already taken.</summary>
    /// <param name="slug">The slug to check.</param>
    /// <param name="excludingId">A tenant to ignore, used when updating that tenant.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when the slug is in use.</returns>
    Task<bool> SlugExistsAsync(
        string slug,
        Guid? excludingId = null,
        CancellationToken cancellationToken = default);
}

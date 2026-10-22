namespace AdNoir.BuildingBlocks.Context;

/// <summary>
/// The tenant the current operation belongs to. Registered as a scoped
/// service and populated from the <c>tenant_id</c> claim for HTTP requests, or from the
/// message's tenant field inside a consumer, where there is no HTTP context.
/// </summary>
public interface ITenantContext
{
    /// <summary>Whether a tenant has been resolved for this scope.</summary>
    bool HasTenant { get; }

    /// <summary>
    /// The current tenant.
    /// </summary>
    /// <remarks>
    /// Throws rather than returning <see cref="Guid.Empty"/> when no tenant was resolved.
    /// An empty GUID would silently build a filter that matches nothing — or, worse, matches
    /// records written under the same empty value — and the mistake would surface as missing
    /// data rather than as an error.
    /// </remarks>
    /// <exception cref="InvalidOperationException">No tenant has been resolved.</exception>
    Guid TenantId { get; }

    /// <summary>
    /// Whether hierarchy is enabled for this tenant. When false, queries filter on
    /// <c>TenantId</c> alone.
    /// </summary>
    bool HierarchyEnabled { get; }

    /// <summary>Sets the tenant for this scope. Used by request middleware and consumer scopes.</summary>
    /// <param name="tenantId">The resolved tenant.</param>
    /// <param name="hierarchyEnabled">Whether hierarchy is enabled for the tenant.</param>
    void SetTenant(Guid tenantId, bool hierarchyEnabled);
}

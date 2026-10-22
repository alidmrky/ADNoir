namespace AdNoir.BuildingBlocks.Context;

/// <summary>
/// Scoped implementation of <see cref="ITenantContext"/>. Populated once per scope: by the
/// tenant resolution middleware for HTTP requests, or explicitly when a consumer opens a
/// scope for an incoming message.
/// </summary>
public sealed class TenantContext : ITenantContext
{
    private Guid? _tenantId;

    /// <inheritdoc />
    public bool HasTenant => _tenantId.HasValue;

    /// <inheritdoc />
    public Guid TenantId =>
        _tenantId ?? throw new InvalidOperationException(
            "No tenant has been resolved for this scope. A tenant-scoped query cannot run " +
            "without one; check that the request carried a tenant_id claim, or that the " +
            "consumer opened its scope with the message's tenant.");

    /// <inheritdoc />
    public bool HierarchyEnabled { get; private set; }

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="tenantId"/> is empty.</exception>
    public void SetTenant(Guid tenantId, bool hierarchyEnabled)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant identifier cannot be empty.", nameof(tenantId));
        }

        _tenantId = tenantId;
        HierarchyEnabled = hierarchyEnabled;
    }
}

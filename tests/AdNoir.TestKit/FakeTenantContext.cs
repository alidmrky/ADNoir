using AdNoir.BuildingBlocks.Context;

namespace AdNoir.TestKit;

/// <summary>Test double for <see cref="ITenantContext"/>.</summary>
public sealed class FakeTenantContext : ITenantContext
{
    /// <summary>A fixed tenant identifier, so assertions can reference it directly.</summary>
    public static readonly Guid DefaultTenantId = Guid.Parse("0192f000-0000-7000-8000-000000000001");

    private Guid? _tenantId = DefaultTenantId;

    /// <inheritdoc />
    public bool HasTenant => _tenantId.HasValue;

    /// <inheritdoc />
    public Guid TenantId =>
        _tenantId ?? throw new InvalidOperationException("No tenant has been resolved for this scope.");

    /// <inheritdoc />
    public bool HierarchyEnabled { get; private set; }

    /// <inheritdoc />
    public void SetTenant(Guid tenantId, bool hierarchyEnabled)
    {
        _tenantId = tenantId;
        HierarchyEnabled = hierarchyEnabled;
    }

    /// <summary>Clears the tenant, to test the unresolved path.</summary>
    public void Clear() => _tenantId = null;
}

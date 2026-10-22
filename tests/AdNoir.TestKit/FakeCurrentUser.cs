using AdNoir.BuildingBlocks.Context;
using AdNoir.Domain.Abstractions.Repositories;

namespace AdNoir.TestKit;

/// <summary>Test double for <see cref="ICurrentUser"/>.</summary>
public sealed class FakeCurrentUser : ICurrentUser
{
    /// <summary>A fixed user identifier, so assertions can reference it directly.</summary>
    public static readonly Guid DefaultUserId = Guid.Parse("0192f000-0000-7000-8000-000000000002");

    /// <summary>A fixed organisation unit identifier.</summary>
    public static readonly Guid DefaultOrgUnitId = Guid.Parse("0192f000-0000-7000-8000-000000000003");

    /// <inheritdoc />
    public bool IsAuthenticated { get; set; } = true;

    /// <inheritdoc />
    public Guid UserId { get; set; } = DefaultUserId;

    /// <inheritdoc />
    public string? Email { get; set; } = "test@adnoir.local";

    /// <inheritdoc />
    public Guid? OrgUnitId { get; set; } = DefaultOrgUnitId;

    /// <inheritdoc />
    public IReadOnlyList<string> Roles { get; set; } = ["TenantAdmin"];

    /// <inheritdoc />
    public long PermissionVersion { get; set; } = 1;

    /// <inheritdoc />
    public string? SecurityStamp { get; set; } = "test-stamp";

    /// <inheritdoc />
    public string? HierarchyPath { get; set; } = "/root/";

    /// <inheritdoc />
    public DataScope ReadScope { get; set; } = DataScope.Tenant;

    /// <inheritdoc />
    public DataScope WriteScope { get; set; } = DataScope.Tenant;
}

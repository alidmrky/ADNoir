using AdNoir.BuildingBlocks.Authorization;

namespace AdNoir.IdentityAndOrg.Api.Grpc;

/// <summary>Explicit service permission and tenant allowlist for permission matrix access.</summary>
public sealed class PermissionMatrixCaller
{
    /// <summary>Dedicated caller credential.</summary>
    public ServiceCredential Credential { get; set; } = new();
    /// <summary>Allows permission matrix reads; authentication alone does not grant access.</summary>
    public bool CanReadPermissionMatrix { get; set; }
    /// <summary>Allows live user-session validation in the same tenant allowlist.</summary>
    public bool CanValidateUserSession { get; set; }
    /// <summary>Explicit opt-in for shared services serving every tenant.</summary>
    public bool AllTenants { get; set; }
    /// <summary>Tenant allowlist when AllTenants is false.</summary>
    public Guid[] TenantIds { get; set; } = [];
}

namespace AdNoir.BuildingBlocks.Context;

/// <summary>
/// Claim names carried by AdNoir access tokens.
/// </summary>
/// <remarks>
/// The token is signed, not encrypted. Fields such as <c>hierarchy_path</c> are therefore
/// readable by anyone holding the token; that is a deliberate trade-off and is stated in the
/// README rather than being quietly assumed.
/// </remarks>
public static class AdNoirClaimTypes
{
    /// <summary>Subject: the user identifier.</summary>
    public const string Subject = "sub";

    /// <summary>The user's e-mail address.</summary>
    public const string Email = "email";

    /// <summary>The tenant the user belongs to.</summary>
    public const string TenantId = "tenant_id";

    /// <summary>
    /// The user's organisation unit. Required: without it the <c>Self</c> and <c>Unit</c>
    /// data scopes cannot be turned into query filters.
    /// </summary>
    public const string OrgUnitId = "org_unit_id";

    /// <summary>Role codes. Permissions themselves are never written into the token.</summary>
    public const string Roles = "roles";

    /// <summary>
    /// Version of the tenant's permission matrix. Part of the cache key for the
    /// role-to-permission map, so bumping it invalidates the cache on its own.
    /// </summary>
    public const string PermissionVersion = "perm_ver";

    /// <summary>The user's hierarchy path, for display.</summary>
    public const string HierarchyPath = "hierarchy_path";

    /// <summary>Whether hierarchy is enabled for the tenant.</summary>
    public const string HierarchyEnabled = "hierarchy_enabled";

    /// <summary>The user's read scope.</summary>
    public const string ReadScope = "read_scope";

    /// <summary>The user's write scope.</summary>
    public const string WriteScope = "write_scope";

    /// <summary>
    /// Security stamp. Changes whenever the user's authorization changes; a token whose stamp
    /// no longer matches is reissued with fresh claims on refresh.
    /// </summary>
    public const string SecurityStamp = "stamp";
}

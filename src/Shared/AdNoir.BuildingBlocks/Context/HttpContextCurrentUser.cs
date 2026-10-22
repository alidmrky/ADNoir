using System.Globalization;
using System.Security.Claims;
using AdNoir.Domain.Abstractions.Repositories;
using Microsoft.AspNetCore.Http;

namespace AdNoir.BuildingBlocks.Context;

/// <summary>
/// Reads the caller from the current HTTP request's claims.
/// </summary>
/// <remarks>
/// Values are read from signed claims. UserSessionMiddleware separately validates live
/// account state before protected requests use these claims.
/// </remarks>
public sealed class HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    /// <inheritdoc />
    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    /// <inheritdoc />
    public Guid UserId =>
        TryGetGuid(AdNoirClaimTypes.Subject)
        ?? TryGetGuid(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException(
            "The current request is not authenticated, so there is no user identifier.");

    /// <inheritdoc />
    public string? Email =>
        FindFirst(AdNoirClaimTypes.Email) ?? FindFirst(ClaimTypes.Email);

    /// <inheritdoc />
    public Guid? OrgUnitId => TryGetGuid(AdNoirClaimTypes.OrgUnitId);

    /// <inheritdoc />
    public IReadOnlyList<string> Roles =>
        Principal is null
            ? []
            : [.. Principal.FindAll(AdNoirClaimTypes.Roles).Select(c => c.Value),
               .. Principal.FindAll(ClaimTypes.Role).Select(c => c.Value)];

    /// <inheritdoc />
    public long PermissionVersion =>
        long.TryParse(FindFirst(AdNoirClaimTypes.PermissionVersion), CultureInfo.InvariantCulture, out long version)
            ? version
            : 0L;

    /// <inheritdoc />
    public string? SecurityStamp => FindFirst(AdNoirClaimTypes.SecurityStamp);

    /// <inheritdoc />
    public string? HierarchyPath => FindFirst(AdNoirClaimTypes.HierarchyPath);

    /// <inheritdoc />
    public DataScope ReadScope => ParseScope(AdNoirClaimTypes.ReadScope, DataScope.Self);

    /// <inheritdoc />
    public DataScope WriteScope => ParseScope(AdNoirClaimTypes.WriteScope, DataScope.Self);

    private string? FindFirst(string claimType) => Principal?.FindFirst(claimType)?.Value;

    private Guid? TryGetGuid(string claimType) =>
        Guid.TryParse(FindFirst(claimType), out Guid value) ? value : null;

    /// <summary>
    /// Parses a scope claim, defaulting to the narrowest scope. An unreadable or missing
    /// scope must never widen access, so the fallback is <see cref="DataScope.Self"/> rather
    /// than <see cref="DataScope.Tenant"/>.
    /// </summary>
    private DataScope ParseScope(string claimType, DataScope fallback) =>
        Enum.TryParse(FindFirst(claimType), ignoreCase: true, out DataScope scope) ? scope : fallback;
}

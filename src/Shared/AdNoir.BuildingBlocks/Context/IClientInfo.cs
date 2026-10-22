using Microsoft.AspNetCore.Http;

namespace AdNoir.BuildingBlocks.Context;

/// <summary>
/// Where the current request came from. Used by the audit trail and by
/// refresh token records, where it is the trail an incident is reconstructed from.
/// </summary>
public interface IClientInfo
{
    /// <summary>Caller IP address, when it can be determined.</summary>
    string? IpAddress { get; }

    /// <summary>Caller user agent, when supplied.</summary>
    string? UserAgent { get; }
}

/// <summary>Reads client information from the current HTTP request.</summary>
public sealed class HttpContextClientInfo(IHttpContextAccessor httpContextAccessor) : IClientInfo
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    /// <inheritdoc />
    /// <remarks>
    /// Reads the socket's remote address, not <c>X-Forwarded-For</c>. Behind the gateway that
    /// header will matter, but trusting it before forwarded-headers middleware is configured
    /// would let any caller write their own IP into the audit trail.
    /// </remarks>
    public string? IpAddress =>
        _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    /// <inheritdoc />
    public string? UserAgent =>
        _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString() is { Length: > 0 } agent
            ? agent
            : null;
}

/// <summary>Client information for work with no request behind it: jobs, consumers, the migrator.</summary>
public sealed class NullClientInfo : IClientInfo
{
    /// <inheritdoc />
    public string? IpAddress => null;

    /// <inheritdoc />
    public string? UserAgent => null;
}

namespace AdNoir.Gateway.Configuration;

/// <summary>Gateway CORS and request budget settings.</summary>
public sealed class GatewayOptions
{
    /// <summary>Configuration section.</summary>
    public const string SectionName = "AdNoir:Gateway";
    /// <summary>Explicit browser origins. An empty list denies cross-origin access.</summary>
    public string[] AllowedOrigins { get; set; } = [];
    /// <summary>Shared Redis key prefix, identical across gateway instances.</summary>
    public string RateLimitPrefix { get; set; } = "adnoir:gateway";
    /// <summary>Sliding window length in seconds.</summary>
    public int WindowSeconds { get; set; } = 60;
    /// <summary>Combined IP budget for login, registration and refresh, including authenticated callers.</summary>
    public int AuthenticationPermitLimit { get; set; } = 10;
    /// <summary>IP budget for other anonymous requests.</summary>
    public int AnonymousPermitLimit { get; set; } = 30;
    /// <summary>Tenant and user budget for authenticated requests.</summary>
    public int AuthenticatedPermitLimit { get; set; } = 300;
}

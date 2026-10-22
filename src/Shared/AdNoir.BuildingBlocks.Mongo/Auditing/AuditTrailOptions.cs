namespace AdNoir.BuildingBlocks.Mongo.Auditing;

/// <summary>
/// Audit trail settings, bound from <c>AdNoir:Audit</c>.
/// </summary>
public sealed class AuditTrailOptions
{
    /// <summary>The configuration section these options bind to.</summary>
    public const string SectionName = "AdNoir:Audit";

    /// <summary>The value written in place of a sensitive field's contents.</summary>
    public const string MaskedValue = "***";

    /// <summary>Collection name for audit records.</summary>
    public string CollectionName { get; set; } = "auditLogs";

    /// <summary>
    /// Field names whose values are never stored. Matching is case-insensitive and by
    /// substring, so <c>passwordHash</c> and <c>PasswordHash</c> are both caught by
    /// <c>password</c>.
    /// </summary>
    /// <remarks>
    /// Masking is mandatory, and this list is why: without it the audit trail
    /// becomes the one place in the system where password hashes and refresh tokens are
    /// stored in plain sight, retained for months, and readable by anyone with the audit
    /// permission. Additions here are cheap; forgetting one is not.
    /// </remarks>
    public IList<string> SensitiveFieldNames { get; set; } =
    [
        "password",
        "passwordhash",
        "token",
        "refreshtoken",
        "secret",
        "privatekey",
        "apikey",
        "securitystamp",
    ];

    /// <summary>How long audit records are kept. Enforced by a TTL index.</summary>
    public TimeSpan Retention { get; set; } = TimeSpan.FromDays(365);

    /// <summary>
    /// Bound of the in-memory queue. When it fills, the writer drops the oldest entry rather
    /// than blocking the request that produced it - audit logging must not become a way to
    /// stall writes.
    /// </summary>
    public int QueueCapacity { get; set; } = 10_000;

    /// <summary>How many entries are written per round trip.</summary>
    public int BatchSize { get; set; } = 100;
}

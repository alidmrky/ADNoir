namespace AdNoir.Shared.Contracts.Events;

/// <summary>
/// Raised when a user's roles change. Services holding a read model of the user
/// refresh it from this event; the change reaches every service within one access-token
/// lifetime because the refresh endpoint reissues claims on a security stamp mismatch.
/// </summary>
public sealed record UserRoleUpdatedEvent : IntegrationEvent
{
    /// <summary>The user whose roles changed.</summary>
    public required Guid UserId { get; init; }

    /// <summary>The user's role codes after the change.</summary>
    public required IReadOnlyList<string> RoleCodes { get; init; }

    /// <summary>
    /// The tenant's permission-matrix version after the change. Cache keys include this
    /// value, so incrementing it invalidates the role-to-permission map by itself.
    /// </summary>
    public required long PermissionVersion { get; init; }

    /// <summary>
    /// The user's new security stamp. A token whose stamp does not match is refreshed with
    /// new claims.
    /// </summary>
    public required string SecurityStamp { get; init; }
}

namespace AdNoir.Shared.Contracts.Events;

/// <summary>A committed account registration; contains no password or authentication token.</summary>
public sealed record UserRegisteredEvent : IntegrationEvent
{
    /// <summary>Destination address; broker access and retention must protect this personal data.</summary>
    public required string Email { get; init; }
    /// <summary>Recipient display name for the welcome template.</summary>
    public required string DisplayName { get; init; }
    /// <summary>Requested language, restricted to the supported welcome catalogs.</summary>
    public string Culture { get; init; } = "en";
}

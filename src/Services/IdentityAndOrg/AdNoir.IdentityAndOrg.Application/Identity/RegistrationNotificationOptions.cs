namespace AdNoir.IdentityAndOrg.Application.Identity;

/// <summary>Explicit producer opt-in, independent of notification service availability.</summary>
public sealed class RegistrationNotificationOptions
{
    /// <summary>Publishes a welcome event in the account transaction when enabled.</summary>
    public bool PublishWelcomeEvent { get; set; }
}

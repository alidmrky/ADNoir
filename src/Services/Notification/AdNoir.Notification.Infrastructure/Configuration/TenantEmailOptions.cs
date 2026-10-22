namespace AdNoir.Notification.Infrastructure.Configuration;

/// <summary>Mail feature activation is separate from future end-user marketing preferences.</summary>
public sealed class TenantEmailOptions
{
    /// <summary>Enables welcome mail for this tenant.</summary>
    public bool Enabled { get; set; }
    /// <summary>Optional complete language-specific welcome overrides.</summary>
    public Dictionary<string, WelcomeTemplate> Templates { get; set; } = [];
}

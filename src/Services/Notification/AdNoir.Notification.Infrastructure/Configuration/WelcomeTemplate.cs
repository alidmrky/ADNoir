namespace AdNoir.Notification.Infrastructure.Configuration;

/// <summary>A versioned plain-text template. Only the displayName placeholder is supported.</summary>
public sealed class WelcomeTemplate
{
    /// <summary>Explicit template revision.</summary>
    public string Version { get; set; } = string.Empty;
    /// <summary>Subject, without newlines.</summary>
    public string Subject { get; set; } = string.Empty;
    /// <summary>Plain-text body.</summary>
    public string Body { get; set; } = string.Empty;
}

namespace AdNoir.Notification.Infrastructure.Configuration;

/// <summary>One centrally managed SMTP provider; credentials are environment-only secrets.</summary>
public sealed class SmtpOptions
{
    /// <summary>SMTP host.</summary>
    public string Host { get; set; } = string.Empty;
    /// <summary>SMTP port, using STARTTLS rather than implicit TLS.</summary>
    public int Port { get; set; } = 587;
    /// <summary>Require STARTTLS with normal certificate validation.</summary>
    public bool EnableSsl { get; set; } = true;
    /// <summary>Sender address controlled by deployment configuration.</summary>
    public string From { get; set; } = string.Empty;
    /// <summary>Optional SMTP username.</summary>
    public string? Username { get; set; }
    /// <summary>SMTP password; never stored in delivery records.</summary>
    public string? Password { get; set; }
    /// <summary>Maximum SMTP operation duration.</summary>
    public int TimeoutSeconds { get; set; } = 30;
}

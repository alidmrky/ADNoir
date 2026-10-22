namespace AdNoir.Notification.Infrastructure.Configuration;

/// <summary>Explicit opt-in; global pause preserves queued records.</summary>
public sealed class NotificationOptions
{
    /// <summary>Configuration section.</summary>
    public const string SectionName = "AdNoir:Notification";
    /// <summary>Starts consumption and sending only when explicitly enabled.</summary>
    public bool Enabled { get; set; }
    /// <summary>Maximum attempts including the first one.</summary>
    public int MaxAttempts { get; set; } = 5;
    /// <summary>Terminal personal-content retention in days.</summary>
    public int RetentionDays { get; set; } = 30;
    /// <summary>Worker lease longer than the SMTP deadline.</summary>
    public int LeaseSeconds { get; set; } = 120;
    /// <summary>Polling interval; one message per configured tenant per tick per instance.</summary>
    public int PollSeconds { get; set; } = 5;
    /// <summary>SMTP connection settings.</summary>
    public SmtpOptions Smtp { get; set; } = new();
    /// <summary>Tenant GUID keys. Unknown tenants are disabled by default.</summary>
    public Dictionary<string, TenantEmailOptions> Tenants { get; set; } = [];
}

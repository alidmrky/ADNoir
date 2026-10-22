namespace AdNoir.IdentityAndOrg.Infrastructure.Seeding;

/// <summary>
/// Bootstrap data settings, bound from <c>AdNoir:Seed</c>.
/// </summary>
public sealed class SeedOptions
{
    /// <summary>The configuration section these options bind to.</summary>
    public const string SectionName = "AdNoir:Seed";

    /// <summary>Display name of the root tenant.</summary>
    public string RootTenantName { get; set; } = "AdNoir";

    /// <summary>Slug of the root tenant, used when signing in.</summary>
    public string RootTenantSlug { get; set; } = "adnoir";

    /// <summary>Display name of the root organisation unit.</summary>
    public string RootOrgUnitName { get; set; } = "Head Office";

    /// <summary>E-mail address of the first administrator.</summary>
    public string SuperAdminEmail { get; set; } = "admin@adnoir.local";

    /// <summary>
    /// Password for the first administrator.
    /// </summary>
    /// <remarks>
    /// Never hard-coded. When empty the migrator generates one, prints it to the console
    /// exactly once and sets <c>MustChangePassword</c>, so a deployment that nobody watched
    /// cannot leave a known password behind.
    /// </remarks>
    public string? SuperAdminPassword { get; set; }

    /// <summary>Display name of the first administrator.</summary>
    public string SuperAdminDisplayName { get; set; } = "System Administrator";
}

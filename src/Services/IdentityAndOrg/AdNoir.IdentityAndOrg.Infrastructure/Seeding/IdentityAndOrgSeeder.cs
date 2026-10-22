using System.Security.Cryptography;
using AdNoir.BuildingBlocks.Context;
using AdNoir.Domain.Abstractions.Repositories;
using AdNoir.IdentityAndOrg.Application.Identity.Abstractions;
using AdNoir.IdentityAndOrg.Domain.Hierarchy;
using AdNoir.IdentityAndOrg.Domain.Identity;
using AdNoir.IdentityAndOrg.Domain.OrgUnits;
using AdNoir.IdentityAndOrg.Domain.Tenants;
using AdNoir.Shared.Contracts.Permissions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AdNoir.IdentityAndOrg.Infrastructure.Seeding;

/// <summary>What the seeder did, so the caller can report it.</summary>
/// <param name="GeneratedPassword">
/// The password that was generated, or null when one was configured. Printed once and never
/// stored anywhere else.
/// </param>
/// <param name="CreatedTenant">Whether the root tenant was created by this run.</param>
/// <param name="CreatedAdmin">Whether the administrator account was created by this run.</param>
/// <param name="UpdatedSystemRoles">Whether system role permissions were brought up to date.</param>
public sealed record SeedResult(
    string? GeneratedPassword,
    bool CreatedTenant,
    bool CreatedAdmin,
    bool UpdatedSystemRoles);

/// <summary>
/// Creates the data without which the system cannot be used at all.
/// </summary>
/// <remarks>
/// <para>
/// An empty database has no users, so nobody can sign in and no protected endpoint can even
/// be tried. That failure is silent - nothing errors, it just does not work - which is why
/// seeding is a required setup step rather than an optional extra.
/// </para>
/// <para>
/// Idempotent by construction: every step checks before it writes, so re-running after adding
/// a permission updates the system roles and leaves everything else untouched.
/// </para>
/// </remarks>
public sealed partial class IdentityAndOrgSeeder(
    ITenantRepository tenantRepository,
    IOrgUnitRepository orgUnitRepository,
    IRoleRepository roleRepository,
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    IOptions<SeedOptions> options,
    ILogger<IdentityAndOrgSeeder> logger)
{
    private readonly ITenantRepository _tenantRepository = tenantRepository;
    private readonly IOrgUnitRepository _orgUnitRepository = orgUnitRepository;
    private readonly IRoleRepository _roleRepository = roleRepository;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;
    private readonly ITenantContext _tenantContext = tenantContext;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly SeedOptions _options = options.Value;
    private readonly ILogger<IdentityAndOrgSeeder> _logger = logger;

    /// <summary>Runs the seed.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>What was created or updated.</returns>
    public async Task<SeedResult> SeedAsync(CancellationToken cancellationToken = default)
    {
        await _unitOfWork.BeginAsync(cancellationToken);

        bool updatedRoles = await EnsureSystemRolesAsync(cancellationToken);
        (Tenant tenant, bool createdTenant) = await EnsureRootTenantAsync(cancellationToken);

        // Every step after this one writes tenant-scoped data, so the scope has to exist
        // before they run. There is no request to take it from here.
        _tenantContext.SetTenant(tenant.Id, tenant.HierarchyEnabled);

        OrgUnit rootUnit = await EnsureRootOrgUnitAsync(tenant, cancellationToken);
        (bool createdAdmin, string? generatedPassword) = await EnsureSuperAdminAsync(
            tenant,
            rootUnit,
            cancellationToken);

        await _unitOfWork.CommitAsync(cancellationToken);

        return new SeedResult(generatedPassword, createdTenant, createdAdmin, updatedRoles);
    }

    /// <summary>
    /// Creates the system roles, and refreshes their permissions on every run.
    /// </summary>
    /// <remarks>
    /// SuperAdmin holds whatever the catalog currently contains. That is what makes adding a
    /// permission and re-running the migrator update the catalog - without it, a new
    /// permission would exist but nobody would hold it.
    /// </remarks>
    private async Task<bool> EnsureSystemRolesAsync(CancellationToken cancellationToken)
    {
        bool updated = false;

        IReadOnlyList<string> allPermissions = PermissionCatalog.All();

        IReadOnlyList<string> tenantAdminPermissions =
        [
            .. allPermissions.Where(permission => !permission.StartsWith("tenant:", StringComparison.Ordinal)),
            PermissionCatalog.Tenants.Read,
        ];

        updated |= await EnsureSystemRoleAsync(
            SystemRoles.SuperAdmin,
            "Platform Administrator",
            allPermissions,
            cancellationToken);

        updated |= await EnsureSystemRoleAsync(
            SystemRoles.TenantAdmin,
            "Tenant Administrator",
            [.. tenantAdminPermissions.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
            cancellationToken);

        return updated;
    }

    private async Task<bool> EnsureSystemRoleAsync(
        string code,
        string name,
        IReadOnlyList<string> permissions,
        CancellationToken cancellationToken)
    {
        Role? existing = await _roleRepository.FindAsync(
            role => role.Code == code && role.TenantId == null,
            cancellationToken);

        if (existing is null)
        {
            Role role = Role.CreateSystemRole(code, name, permissions);
            await _roleRepository.InsertAsync(role, cancellationToken);

            _logger.LogCreatedSystemRole(code, permissions.Count);

            return true;
        }

        if (existing.Permissions.SequenceEqual(permissions, StringComparer.Ordinal))
        {
            return false;
        }

        existing.Permissions = permissions;
        await _roleRepository.UpdateAsync(existing, cancellationToken);

        _logger.LogUpdatedSystemRole(code, permissions.Count);

        return true;
    }

    private async Task<(Tenant Tenant, bool Created)> EnsureRootTenantAsync(CancellationToken cancellationToken)
    {
        Tenant? existing = await _tenantRepository.FindBySlugAsync(_options.RootTenantSlug, cancellationToken);

        if (existing is not null)
        {
            return (existing, false);
        }

        Tenant tenant = Tenant.Create(
            _options.RootTenantName,
            _options.RootTenantSlug,
            hierarchyEnabled: true);

        tenant.Status = TenantStatus.Active;

        await _tenantRepository.InsertAsync(tenant, cancellationToken);

        _logger.LogCreatedRootTenant(tenant.Slug, tenant.Id);

        return (tenant, true);
    }

    private async Task<OrgUnit> EnsureRootOrgUnitAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        IReadOnlyList<OrgUnit> roots = await _orgUnitRepository.ListRootsAsync(cancellationToken);

        if (roots.Count > 0)
        {
            return roots[0];
        }

        OrgUnit unit = OrgUnit.CreateRoot(
            tenant.Id,
            _options.RootOrgUnitName,
            Slug.From(_options.RootOrgUnitName));

        await _orgUnitRepository.InsertAsync(unit, cancellationToken);

        _logger.LogCreatedRootOrgUnit(unit.HierarchyPath, unit.Id);

        return unit;
    }

    private async Task<(bool Created, string? GeneratedPassword)> EnsureSuperAdminAsync(
        Tenant tenant,
        OrgUnit rootUnit,
        CancellationToken cancellationToken)
    {
        string normalizedEmail = User.NormalizeEmail(_options.SuperAdminEmail);

        User? existing = await _userRepository.FindByNormalizedEmailAsync(normalizedEmail, cancellationToken);

        if (existing is not null)
        {
            return (false, null);
        }

        Role? superAdmin = await _roleRepository.FindAsync(
            role => role.Code == SystemRoles.SuperAdmin && role.TenantId == null,
            cancellationToken);

        if (superAdmin is null)
        {
            throw new InvalidOperationException(
                "The SuperAdmin role is missing even though the seeder just created it. " +
                "This indicates the role write was not visible to the subsequent read.");
        }

        bool passwordWasConfigured = !string.IsNullOrWhiteSpace(_options.SuperAdminPassword);
        string password = passwordWasConfigured ? _options.SuperAdminPassword! : GeneratePassword();

        User admin = User.Create(
            tenant.Id,
            rootUnit.Id,
            _options.SuperAdminEmail,
            _options.SuperAdminDisplayName,
            _passwordHasher.Hash(password));

        admin.EmailConfirmed = true;

        // A generated password has been printed to a console that may be scrolled away or
        // captured in a deployment log, so it is treated as compromised from the start.
        admin.MustChangePassword = !passwordWasConfigured;

        admin.RoleAssignments =
        [
            new RoleAssignment(
                superAdmin.Id,
                superAdmin.Code,
                DataScope.Tenant,
                DataScope.Tenant),
        ];

        await _userRepository.InsertAsync(admin, cancellationToken);

        _logger.LogCreatedAdministrator(admin.Email, admin.Id, tenant.Slug);

        return (true, passwordWasConfigured ? null : password);
    }

    /// <summary>
    /// Generates a readable but strong password.
    /// </summary>
    /// <remarks>
    /// Characters that are easy to confuse when copied off a terminal - <c>0/O</c>,
    /// <c>1/l/I</c> - are left out. A password nobody can transcribe correctly gets replaced
    /// with a weak one.
    /// </remarks>
    private static string GeneratePassword()
    {
        const string alphabet = "abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789-_";
        const int length = 24;

        return RandomNumberGenerator.GetString(alphabet, length);
    }
}

/// <summary>Static LoggerMessage definitions for IdentityAndOrgSeeder (CA1873).</summary>
internal static partial class IdentityAndOrgSeederLogMessages
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Created system role {RoleCode} with {Count} permissions.")]
    public static partial void LogCreatedSystemRole(this ILogger logger, string roleCode, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Updated system role {RoleCode} to {Count} permissions.")]
    public static partial void LogUpdatedSystemRole(this ILogger logger, string roleCode, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Created root tenant {Slug} ({TenantId}).")]
    public static partial void LogCreatedRootTenant(this ILogger logger, string slug, Guid tenantId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Created root organisation unit {Path} ({OrgUnitId}).")]
    public static partial void LogCreatedRootOrgUnit(this ILogger logger, string path, Guid orgUnitId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Created administrator {Email} ({UserId}) in tenant {Slug}.")]
    public static partial void LogCreatedAdministrator(this ILogger logger, string email, Guid userId, string slug);
}

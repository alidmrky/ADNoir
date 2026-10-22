using System.Text.Json;
using AdNoir.BuildingBlocks.Auditing;
using AdNoir.IdentityAndOrg.Domain.Identity;
using AdNoir.IdentityAndOrg.Domain.Tenants;

namespace AdNoir.IdentityAndOrg.Application.Identity;

/// <summary>Explicit business-field allowlists; never serialize users or request DTOs wholesale.</summary>
internal static class IdentityAuditFields
{
    internal static Dictionary<string, string?> Capture(User user) => new(StringComparer.Ordinal)
    {
        [nameof(User.IsActive)] = user.IsActive.ToString(),
        [nameof(User.OrgUnitId)] = user.OrgUnitId.ToString(),
        [nameof(User.MustChangePassword)] = user.MustChangePassword.ToString(),
        [nameof(User.RoleAssignments)] = JsonSerializer.Serialize(user.RoleAssignments.OrderBy(role => role.RoleId)),
    };

    internal static Dictionary<string, string?> Capture(Role role) => new(StringComparer.Ordinal)
    {
        [nameof(Role.Version)] = role.Version.ToString(System.Globalization.CultureInfo.InvariantCulture),
        [nameof(Role.Code)] = role.Code,
        [nameof(Role.Name)] = role.Name,
        [nameof(Role.Permissions)] = JsonSerializer.Serialize(role.Permissions.Order(StringComparer.Ordinal)),
        [nameof(Role.IsDeleted)] = role.IsDeleted.ToString(),
    };

    internal static Dictionary<string, string?> Capture(Tenant tenant) => new(StringComparer.Ordinal)
    {
        [nameof(Tenant.Name)] = tenant.Name,
        [nameof(Tenant.Slug)] = tenant.Slug,
        [nameof(Tenant.Status)] = tenant.Status.ToString(),
        [nameof(Tenant.TimeZoneId)] = tenant.TimeZoneId,
        [nameof(Tenant.HierarchyEnabled)] = tenant.HierarchyEnabled.ToString(),
    };

    internal static IReadOnlyList<FieldChange> Diff(Dictionary<string, string?>? before,
        Dictionary<string, string?> after) => after.Select(field => new FieldChange(field.Key,
            before?.GetValueOrDefault(field.Key), field.Value)).ToArray();
}

using System.Text.Json;
using AdNoir.BuildingBlocks.Auditing;
using AdNoir.IdentityAndOrg.Domain.OrgUnits;

namespace AdNoir.IdentityAndOrg.Application.OrgUnits;

/// <summary>Explicit structural and management fields for organisation audit history.</summary>
internal static class OrgUnitAuditFields
{
    internal static Dictionary<string, string?> Capture(OrgUnit unit) => new(StringComparer.Ordinal)
    {
        [nameof(OrgUnit.Name)] = unit.Name,
        [nameof(OrgUnit.Code)] = unit.Code,
        [nameof(OrgUnit.Slug)] = unit.Slug,
        [nameof(OrgUnit.ParentId)] = unit.ParentId?.ToString(),
        [nameof(OrgUnit.HierarchyPath)] = unit.HierarchyPath,
        [nameof(OrgUnit.Ancestors)] = JsonSerializer.Serialize(unit.Ancestors),
        [nameof(OrgUnit.IsDeleted)] = unit.IsDeleted.ToString(),
    };

    internal static IReadOnlyList<FieldChange> Diff(Dictionary<string, string?>? before, OrgUnit unit) =>
        Capture(unit).Select(field => new FieldChange(field.Key, before?.GetValueOrDefault(field.Key), field.Value)).ToArray();
}

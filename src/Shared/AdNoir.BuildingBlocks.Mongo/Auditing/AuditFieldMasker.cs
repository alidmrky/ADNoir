using Microsoft.Extensions.Options;

namespace AdNoir.BuildingBlocks.Mongo.Auditing;

/// <summary>
/// Replaces sensitive values before they reach the audit collection.
/// </summary>
/// <remarks>
/// Masking happens on the way in, not on the way out. Storing the real value and hiding it
/// at read time would still leave secrets sitting in the database, in backups, and in
/// anything that copies the collection.
/// </remarks>
public sealed class AuditFieldMasker(IOptions<AuditTrailOptions> options)
{
    private readonly IReadOnlyList<string> _sensitiveNames =
        [.. new AuditTrailOptions().SensitiveFieldNames.Concat(options.Value.SensitiveFieldNames)
            .Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.ToLowerInvariant()).Distinct()];

    /// <summary>Whether a field's value must be masked.</summary>
    /// <param name="fieldName">The field name.</param>
    /// <returns><see langword="true"/> when the name matches the sensitive list.</returns>
    public bool IsSensitive(string fieldName)
    {
        if (string.IsNullOrEmpty(fieldName))
        {
            return false;
        }

        string normalized = fieldName.ToLowerInvariant();

        foreach (string sensitive in _sensitiveNames)
        {
            if (normalized.Contains(sensitive, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Masks the before and after values of a change when the field is sensitive.</summary>
    /// <param name="change">The change.</param>
    /// <returns>The change, masked if necessary.</returns>
    public AuditFieldChange Mask(AuditFieldChange change)
    {
        ArgumentNullException.ThrowIfNull(change);

        if (!IsSensitive(change.Field))
        {
            return change;
        }

        // The fact that a sensitive field changed is worth recording; its contents are not.
        // Masking both sides keeps "the password was changed" auditable without storing it.
        return change with
        {
            Before = change.Before is null ? null : AuditTrailOptions.MaskedValue,
            After = change.After is null ? null : AuditTrailOptions.MaskedValue,
        };
    }

    /// <summary>Masks every change in a set.</summary>
    /// <param name="changes">The changes.</param>
    /// <returns>The masked changes.</returns>
    public IReadOnlyList<AuditFieldChange> MaskAll(IEnumerable<AuditFieldChange> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);

        return [.. changes.Select(Mask)];
    }
}

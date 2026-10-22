namespace AdNoir.BuildingBlocks.Auditing;

/// <summary>Allowlisted, scalar representations of a changed business field.</summary>
public sealed record FieldChange(string Field, string? Before, string? After);

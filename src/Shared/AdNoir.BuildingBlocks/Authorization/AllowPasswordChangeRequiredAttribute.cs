namespace AdNoir.BuildingBlocks.Authorization;

/// <summary>Allows a current session to change its mandatory password without granting business access.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class AllowPasswordChangeRequiredAttribute : Attribute;

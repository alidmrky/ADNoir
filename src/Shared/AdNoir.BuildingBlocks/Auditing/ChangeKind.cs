namespace AdNoir.BuildingBlocks.Auditing;

/// <summary>Business change to record in the current transaction.</summary>
public enum ChangeKind
{
    /// <summary>A record was created.</summary>
    Created,
    /// <summary>A record was updated.</summary>
    Updated,
    /// <summary>A record was deleted.</summary>
    Deleted,
}

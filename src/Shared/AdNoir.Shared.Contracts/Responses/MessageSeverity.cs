namespace AdNoir.Shared.Contracts.Responses;

/// <summary>
/// Severity of a single <see cref="ResponseMessage"/>.
/// </summary>
public enum MessageSeverity
{
    /// <summary>The operation failed because of this message.</summary>
    Error = 0,

    /// <summary>The operation succeeded, but the caller should be told about this.</summary>
    Warning = 1,

    /// <summary>Purely informational.</summary>
    Info = 2,
}

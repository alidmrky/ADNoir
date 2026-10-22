namespace AdNoir.BuildingBlocks.Authorization;

/// <summary>Live authorization state, never cached with the permission matrix.</summary>
public enum UserSessionState
{
    /// <summary>The token no longer represents an active account and current permissions.</summary>
    Invalid = 0,
    /// <summary>The session may access authorized operations.</summary>
    Valid = 1,
    /// <summary>Only password recovery operations may proceed.</summary>
    PasswordChangeRequired = 2,
}

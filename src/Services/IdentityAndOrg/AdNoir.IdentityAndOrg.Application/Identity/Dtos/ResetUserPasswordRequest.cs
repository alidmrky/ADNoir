namespace AdNoir.IdentityAndOrg.Application.Identity.Dtos;

/// <summary>Administrator request to replace a user's forgotten password with a temporary one.</summary>
/// <param name="NewPassword">The temporary password; the user must change it at next sign-in.</param>
/// <param name="ExpectedVersion">The version the caller last read.</param>
public sealed record ResetUserPasswordRequest(string NewPassword, long ExpectedVersion);

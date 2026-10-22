using AdNoir.Domain.Abstractions.Repositories;

namespace AdNoir.IdentityAndOrg.Application.Identity.Dtos;

/// <summary>Tokens returned by sign-in and refresh.</summary>
/// <param name="AccessToken">The signed JWT.</param>
/// <param name="ExpiresInSeconds">Access token lifetime, five minutes by default.</param>
/// <param name="RefreshToken">
/// The refresh token, returned in plaintext exactly once. Only its hash is stored.
/// </param>
/// <param name="MustChangePassword">
/// Whether the user has to change their password before doing anything else. Set by the
/// migrator for the generated first administrator.
/// </param>
/// <param name="TokenType">Always <c>Bearer</c>.</param>
public sealed record AuthTokensDto(
    string AccessToken,
    int ExpiresInSeconds,
    string RefreshToken,
    bool MustChangePassword,
    string TokenType = "Bearer");

/// <summary>A user as exposed by the API. Never carries the password hash or security stamp.</summary>
/// <param name="Id">User identifier.</param>
/// <param name="Email">E-mail address.</param>
/// <param name="DisplayName">Display name.</param>
/// <param name="OrgUnitId">The user's organisation unit.</param>
/// <param name="Roles">Roles granted, with their scopes.</param>
/// <param name="IsActive">Whether the account may be used.</param>
/// <param name="EmailConfirmed">Whether the address has been confirmed.</param>
/// <param name="MustChangePassword">Whether a password change is pending.</param>
/// <param name="LastLoginAtUtc">Last successful sign-in.</param>
/// <param name="Version">Row version, echoed back on update.</param>
public sealed record UserDto(
    Guid Id,
    string Email,
    string DisplayName,
    Guid OrgUnitId,
    IReadOnlyList<RoleAssignmentDto> Roles,
    bool IsActive,
    bool EmailConfirmed,
    bool MustChangePassword,
    DateTime? LastLoginAtUtc,
    long Version);

/// <summary>A role granted to a user and how far it reaches.</summary>
/// <param name="RoleId">The role.</param>
/// <param name="RoleCode">The role code.</param>
/// <param name="ReadScope">Read scope.</param>
/// <param name="WriteScope">Write scope.</param>
public sealed record RoleAssignmentDto(
    Guid RoleId,
    string RoleCode,
    DataScope ReadScope,
    DataScope WriteScope);

/// <summary>A role as exposed by the API.</summary>
/// <param name="Id">Role identifier.</param>
/// <param name="Code">Role code.</param>
/// <param name="Name">Display name.</param>
/// <param name="Permissions">Permissions granted.</param>
/// <param name="IsSystemRole">Whether it is built in and immutable.</param>
/// <param name="Version">Row version.</param>
public sealed record RoleDto(
    Guid Id,
    string Code,
    string Name,
    IReadOnlyList<string> Permissions,
    bool IsSystemRole,
    long Version);

/// <summary>Sign-in request.</summary>
/// <param name="TenantSlug">
/// Which tenant to sign in to. Required because e-mail addresses are unique per tenant, not
/// globally - the same person can hold accounts in two tenants.
/// </param>
/// <param name="Email">E-mail address.</param>
/// <param name="Password">Password.</param>
public sealed record LoginRequest(string TenantSlug, string Email, string Password);

/// <summary>Registration request.</summary>
/// <param name="TenantSlug">The tenant to register into.</param>
/// <param name="Email">E-mail address.</param>
/// <param name="Password">Password.</param>
/// <param name="DisplayName">Display name.</param>
public sealed record RegisterRequest(
    string TenantSlug,
    string Email,
    string Password,
    string DisplayName);

/// <summary>Token renewal request.</summary>
/// <param name="RefreshToken">The refresh token issued previously.</param>
public sealed record RefreshRequest(string RefreshToken);

/// <summary>Sign-out request.</summary>
/// <param name="RefreshToken">The refresh token to revoke.</param>
/// <param name="AllSessions">Whether to revoke every session the user holds.</param>
public sealed record LogoutRequest(string RefreshToken, bool AllSessions = false);

/// <summary>Password change request.</summary>
/// <param name="CurrentPassword">The current password.</param>
/// <param name="NewPassword">The new password.</param>
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

/// <summary>Request to create a user.</summary>
/// <param name="Email">E-mail address.</param>
/// <param name="DisplayName">Display name.</param>
/// <param name="Password">Initial password.</param>
/// <param name="OrgUnitId">The organisation unit the user belongs to.</param>
/// <param name="MustChangePassword">Whether to force a password change at first sign-in.</param>
public sealed record CreateUserRequest(
    string Email,
    string DisplayName,
    string Password,
    Guid OrgUnitId,
    bool MustChangePassword = true);

/// <summary>Request to replace a user's role assignments.</summary>
/// <param name="Assignments">The complete new set of assignments.</param>
/// <param name="ExpectedVersion">The version the caller last read.</param>
/// <remarks>
/// Replaces rather than merges: an "add role" API makes it easy to end up with assignments
/// nobody intended, and the caller already has the full list from the read model.
/// </remarks>
public sealed record AssignRolesRequest(
    IReadOnlyList<RoleAssignmentDto> Assignments,
    long ExpectedVersion);

/// <summary>Request to create a role.</summary>
/// <param name="Code">Role code.</param>
/// <param name="Name">Display name.</param>
/// <param name="Permissions">Permissions granted.</param>
public sealed record CreateRoleRequest(string Code, string Name, IReadOnlyList<string> Permissions);

/// <summary>Request to update a role's name and permissions.</summary>
/// <param name="Name">New display name.</param>
/// <param name="Permissions">New permission set.</param>
/// <param name="ExpectedVersion">The version the caller last read.</param>
public sealed record UpdateRoleRequest(
    string Name,
    IReadOnlyList<string> Permissions,
    long ExpectedVersion);

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace AdNoir.BuildingBlocks.Authorization;

/// <summary>
/// Creates authorization policies for permission names on demand.
/// </summary>
/// <remarks>
/// Registering one named policy per permission at startup would mean editing composition
/// code every time a permission is added - and forgetting to is the kind of mistake that
/// shows up as a 403 in production. Instead any policy named <c>permission:{name}</c> is
/// materialised on first use, so <c>.RequirePermission("orgunit:write")</c> just works.
/// </remarks>
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    /// <summary>Prefix identifying a permission policy.</summary>
    public const string PolicyPrefix = "permission:";

    /// <summary>Builds the policy name for a permission.</summary>
    /// <param name="permission">The permission, e.g. <c>orgunit:write</c>.</param>
    /// <returns>The policy name.</returns>
    public static string PolicyNameFor(string permission) => PolicyPrefix + permission;

    /// <inheritdoc />
    public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);

        if (!policyName.StartsWith(PolicyPrefix, StringComparison.Ordinal))
        {
            return base.GetPolicyAsync(policyName);
        }

        string permission = policyName[PolicyPrefix.Length..];

        AuthorizationPolicy policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(permission))
            .Build();

        return Task.FromResult<AuthorizationPolicy?>(policy);
    }
}

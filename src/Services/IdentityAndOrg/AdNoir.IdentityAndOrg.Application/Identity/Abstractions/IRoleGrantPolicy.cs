using AdNoir.IdentityAndOrg.Application.Identity.Dtos;
using AdNoir.IdentityAndOrg.Domain.Identity;

namespace AdNoir.IdentityAndOrg.Application.Identity.Abstractions;

/// <summary>Restricts delegation to the caller's current permissions and data scopes.</summary>
public interface IRoleGrantPolicy
{
    /// <summary>Checks whether the caller may manage this tenant role definition.</summary>
    Task<bool> CanManageAsync(string code, IReadOnlyList<string> permissions, CancellationToken cancellationToken);

    /// <summary>Checks both removed and added assignments before replacing a user's roles.</summary>
    Task<bool> CanAssignAsync(User target, IReadOnlyList<RoleAssignmentDto> assignments,
        IReadOnlyList<Role> available, CancellationToken cancellationToken);
}

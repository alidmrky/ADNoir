using AdNoir.BuildingBlocks.Auditing;
using AdNoir.BuildingBlocks.Context;
using AdNoir.Domain.Abstractions.Repositories;
using AdNoir.IdentityAndOrg.Application.Identity.Abstractions;
using AdNoir.IdentityAndOrg.Application.Identity.Dtos;
using AdNoir.IdentityAndOrg.Domain.Identity;
using AdNoir.IdentityAndOrg.Domain.Tenants;
using AdNoir.Shared.Contracts.Messages;
using AdNoir.Shared.Contracts.Permissions;
using AdNoir.Shared.Contracts.Responses;

namespace AdNoir.IdentityAndOrg.Application.Identity;

/// <summary>Default <see cref="IRoleService"/>.</summary>
public sealed class RoleService(
    IRoleRepository roleRepository,
    IUserRepository userRepository,
    ITenantRepository tenantRepository,
    ITenantContext tenantContext,
    IRoleGrantPolicy grantPolicy,
    ITransactionalAuditRecorder auditRecorder,
    IUnitOfWork unitOfWork) : IRoleService
{
    private readonly IRoleRepository _roleRepository = roleRepository;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly ITenantRepository _tenantRepository = tenantRepository;
    private readonly ITenantContext _tenantContext = tenantContext;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    /// <inheritdoc />
    public async Task<BaseResponse<IReadOnlyList<RoleDto>>> ListAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Role> roles = await _roleRepository.ListForTenantAsync(
            _tenantContext.TenantId,
            cancellationToken);

        return BaseResponse<IReadOnlyList<RoleDto>>.Success([.. roles.Select(ToDto)]);
    }

    /// <inheritdoc />
    public async Task<BaseResponse<RoleDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Role? role = await _roleRepository.FindByIdAsync(id, cancellationToken);

        return role is null
            ? BaseResponse<RoleDto>.NotFound(MessageCodes.Identity.RoleNotFound, ("roleCode", id))
            : BaseResponse<RoleDto>.Success(ToDto(role));
    }

    /// <inheritdoc />
    public async Task<BaseResponse<RoleDto>> CreateAsync(
        CreateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        BaseResponse? invalid = ValidatePermissions(request.Permissions);

        if (invalid is not null)
        {
            return BaseResponse<RoleDto>.Propagate(invalid);
        }

        if (!await grantPolicy.CanManageAsync(request.Code, request.Permissions, cancellationToken))
        {
            return BaseResponse<RoleDto>.Forbidden(MessageCodes.Auth.PermissionDenied);
        }

        Role? existing = await _roleRepository.FindByCodeAsync(
            _tenantContext.TenantId,
            request.Code,
            cancellationToken);

        if (existing is not null)
        {
            return BaseResponse<RoleDto>.Conflict(MessageCodes.Identity.RoleConflict, ("roleCode", request.Code));
        }

        Role role = Role.CreateForTenant(
            _tenantContext.TenantId,
            request.Code.Trim(),
            request.Name.Trim(),
            request.Permissions);

        await _unitOfWork.BeginAsync(cancellationToken);
        await _roleRepository.InsertAsync(role, cancellationToken);
        await BumpPermissionVersionAsync(cancellationToken);
        await auditRecorder.RecordAsync(_tenantContext.TenantId, role, ChangeKind.Created,
            IdentityAuditFields.Diff(null, IdentityAuditFields.Capture(role)), cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        return BaseResponse<RoleDto>.Created(ToDto(role));
    }

    /// <inheritdoc />
    public async Task<BaseResponse<RoleDto>> UpdateAsync(
        Guid id,
        UpdateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Role? role = await _roleRepository.FindByIdAsync(id, cancellationToken);

        if (role is null)
        {
            return BaseResponse<RoleDto>.NotFound(MessageCodes.Identity.RoleNotFound, ("roleCode", id));
        }

        if (role.IsSystemRole)
        {
            // Seed data and issued tokens reference these codes and permissions; editing them
            // would change what SuperAdmin means platform-wide.
            return BaseResponse<RoleDto>.BusinessRuleViolation(MessageCodes.Identity.SystemRoleImmutable);
        }

        if (!await grantPolicy.CanManageAsync(role.Code, role.Permissions, cancellationToken) ||
            !await grantPolicy.CanManageAsync(role.Code, request.Permissions, cancellationToken))
        {
            return BaseResponse<RoleDto>.Forbidden(MessageCodes.Auth.PermissionDenied);
        }

        if (role.Version != request.ExpectedVersion)
        {
            return BaseResponse<RoleDto>.ConcurrencyConflict();
        }

        BaseResponse? invalid = ValidatePermissions(request.Permissions);

        if (invalid is not null)
        {
            return BaseResponse<RoleDto>.Propagate(invalid);
        }

        var auditBefore = IdentityAuditFields.Capture(role);
        role.Name = request.Name.Trim();
        role.Permissions = request.Permissions;

        await _unitOfWork.BeginAsync(cancellationToken);
        await _roleRepository.UpdateAsync(role, cancellationToken);

        // The matrix changed, so its version changes. Every service's cache key includes that
        // version, which is what makes invalidation happen by itself rather than through an
        // eviction message that could be lost.
        await BumpPermissionVersionAsync(cancellationToken);

        await auditRecorder.RecordAsync(_tenantContext.TenantId, role, ChangeKind.Updated,
            IdentityAuditFields.Diff(auditBefore, IdentityAuditFields.Capture(role)), cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        return BaseResponse<RoleDto>.Success(ToDto(role));
    }

    /// <inheritdoc />
    public async Task<BaseResponse> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Role? role = await _roleRepository.FindByIdAsync(id, cancellationToken);

        if (role is null)
        {
            return BaseResponse.NotFound(MessageCodes.Identity.RoleNotFound, ("roleCode", id));
        }

        if (role.IsSystemRole)
        {
            return BaseResponse.BusinessRuleViolation(MessageCodes.Identity.SystemRoleImmutable);
        }

        if (!await grantPolicy.CanManageAsync(role.Code, role.Permissions, cancellationToken))
        {
            return BaseResponse.Forbidden(MessageCodes.Auth.PermissionDenied);
        }

        IReadOnlyList<User> holders = await _userRepository.ListByRoleAsync(id, cancellationToken);

        if (holders.Count > 0)
        {
            // Deleting a role out from under its holders would silently strip their
            // permissions, and the users would report it as "I lost access" with no trail.
            return BaseResponse.BusinessRuleViolation(
                MessageCodes.OrgUnit.HasChildren,
                ("childCount", holders.Count));
        }

        await _unitOfWork.BeginAsync(cancellationToken);
        await _roleRepository.DeleteAsync(role, cancellationToken);
        await BumpPermissionVersionAsync(cancellationToken);
        await auditRecorder.RecordAsync(_tenantContext.TenantId, role, ChangeKind.Deleted,
            [new FieldChange(nameof(Role.IsDeleted), "False", "True")], cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        return BaseResponse.NoContent();
    }

    /// <summary>
    /// Rejects permissions that are not in the catalog, so a typo cannot create a role that
    /// silently grants nothing.
    /// </summary>
    private static BaseResponse? ValidatePermissions(IReadOnlyList<string> permissions)
    {
        List<ResponseMessage> unknown =
        [
            .. permissions
                .Where(permission => !PermissionCatalog.Exists(permission))
                .Select(permission => ResponseMessage.ForField(
                    MessageCodes.Validation.NotAllowed,
                    nameof(CreateRoleRequest.Permissions),
                    ("value", permission)))
        ];

        return unknown.Count == 0 ? null : BaseResponse.ValidationFailed(unknown);
    }

    private async Task BumpPermissionVersionAsync(CancellationToken cancellationToken)
    {
        Tenant? tenant = await _tenantRepository.FindByIdAsync(_tenantContext.TenantId, cancellationToken);

        if (tenant is null)
        {
            return;
        }

        tenant.BumpPermissionVersion();
        await _tenantRepository.UpdateAsync(tenant, cancellationToken);
    }

    private static RoleDto ToDto(Role role) =>
        new(role.Id, role.Code, role.Name, role.Permissions, role.IsSystemRole, role.Version);
}

using AdNoir.BuildingBlocks.Auditing;
using AdNoir.BuildingBlocks.Context;
using AdNoir.Domain.Abstractions.Repositories;
using AdNoir.IdentityAndOrg.Application.Identity.Abstractions;
using AdNoir.IdentityAndOrg.Application.Identity.Dtos;
using AdNoir.IdentityAndOrg.Domain.Identity;
using AdNoir.IdentityAndOrg.Domain.Tenants;
using AdNoir.Shared.Contracts.Events;
using AdNoir.Shared.Contracts.Messages;
using AdNoir.Shared.Contracts.Responses;

namespace AdNoir.IdentityAndOrg.Application.Identity;

/// <summary>Default <see cref="IUserService"/>.</summary>
public sealed class UserService(
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    ITenantRepository tenantRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IPasswordHasher passwordHasher,
    ITenantContext tenantContext,
    IRoleGrantPolicy grantPolicy,
    IIntegrationEventPublisher eventPublisher,
    ITransactionalAuditRecorder auditRecorder,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IUserService
{
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IRoleRepository _roleRepository = roleRepository;
    private readonly ITenantRepository _tenantRepository = tenantRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository = refreshTokenRepository;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;
    private readonly ITenantContext _tenantContext = tenantContext;
    private readonly IIntegrationEventPublisher _eventPublisher = eventPublisher;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc />
    public async Task<BaseResponse<UserDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        User? user = await _userRepository.FindByIdAsync(id, cancellationToken);

        return user is null
            ? BaseResponse<UserDto>.NotFound(MessageCodes.Identity.UserNotFound, ("userId", id))
            : BaseResponse<UserDto>.Success(ToDto(user));
    }

    /// <inheritdoc />
    public async Task<PagedResponse<UserDto>> ListAsync(
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        PagedResult<User> page = await _userRepository.ListPagedAsync(
            pageNumber,
            pageSize,
            predicate: null,
            orderBy: user => user.NormalizedEmail,
            descending: false,
            cancellationToken);

        return PagedResponse<UserDto>.Create(
            [.. page.Items.Select(ToDto)],
            pageNumber,
            pageSize,
            page.TotalCount);
    }

    /// <inheritdoc />
    public async Task<BaseResponse<UserDto>> CreateAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string normalizedEmail = User.NormalizeEmail(request.Email);

        if (await _userRepository.EmailExistsAsync(normalizedEmail, excludingId: null, cancellationToken))
        {
            return BaseResponse<UserDto>.Conflict(MessageCodes.Identity.EmailConflict, ("email", request.Email));
        }

        User user = User.Create(
            _tenantContext.TenantId,
            request.OrgUnitId,
            request.Email,
            request.DisplayName,
            _passwordHasher.Hash(request.Password));

        user.MustChangePassword = request.MustChangePassword;

        await _unitOfWork.BeginAsync(cancellationToken);
        await _userRepository.InsertAsync(user, cancellationToken);
        await auditRecorder.RecordAsync(user.TenantId, user, ChangeKind.Created,
            IdentityAuditFields.Diff(null, IdentityAuditFields.Capture(user)), cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        return BaseResponse<UserDto>.Created(ToDto(user));
    }

    /// <inheritdoc />
    public async Task<BaseResponse<UserDto>> AssignRolesAsync(
        Guid id,
        AssignRolesRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        User? user = await _userRepository.FindByIdAsync(id, cancellationToken);

        if (user is null)
        {
            return BaseResponse<UserDto>.NotFound(MessageCodes.Identity.UserNotFound, ("userId", id));
        }

        if (user.Version != request.ExpectedVersion)
        {
            return BaseResponse<UserDto>.ConcurrencyConflict();
        }

        IReadOnlyList<Role> available = await _roleRepository.ListForTenantAsync(
            _tenantContext.TenantId,
            cancellationToken);

        if (!await grantPolicy.CanAssignAsync(user, request.Assignments, available, cancellationToken))
        {
            return BaseResponse<UserDto>.Forbidden(MessageCodes.Auth.PermissionDenied);
        }

        Dictionary<Guid, Role> byId = available.ToDictionary(role => role.Id);
        List<RoleAssignment> assignments = new(request.Assignments.Count);

        foreach (RoleAssignmentDto assignment in request.Assignments)
        {
            if (!byId.TryGetValue(assignment.RoleId, out Role? role))
            {
                // Assigning a role the tenant cannot see would grant permissions nobody can
                // review from inside that tenant.
                return BaseResponse<UserDto>.NotFound(
                    MessageCodes.Identity.RoleNotFound,
                    ("roleCode", assignment.RoleId));
            }

            assignments.Add(new RoleAssignment(
                role.Id,
                role.Code,
                assignment.ReadScope,
                assignment.WriteScope));
        }

        var auditBefore = IdentityAuditFields.Capture(user);
        user.RoleAssignments = assignments;

        // Live session validation rejects old access tokens after this stamp change.
        // Refresh can issue claims reflecting the new role assignments.
        user.RotateSecurityStamp();

        Tenant? tenant = await _tenantRepository.FindByIdAsync(_tenantContext.TenantId, cancellationToken);

        await _unitOfWork.BeginAsync(cancellationToken);

        await _userRepository.UpdateAsync(user, cancellationToken);

        // Published before the commit: the outbox record has to land in the same transaction
        // as the data change, and publishing afterwards would put it outside.
        await _eventPublisher.PublishAsync(
            new UserRoleUpdatedEvent
            {
                TenantId = user.TenantId,
                UserId = user.Id,
                RoleCodes = user.RoleCodes,
                PermissionVersion = tenant?.PermissionVersion ?? 0,
                SecurityStamp = user.SecurityStamp,
            },
            cancellationToken);

        await auditRecorder.RecordAsync(user.TenantId, user, ChangeKind.Updated,
            IdentityAuditFields.Diff(auditBefore, IdentityAuditFields.Capture(user))
                .Append(new FieldChange("SecurityStamp", "previous", "rotated")).ToArray(), cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        return BaseResponse<UserDto>.Success(ToDto(user));
    }

    /// <inheritdoc />
    public async Task<BaseResponse<UserDto>> SetActiveAsync(
        Guid id,
        bool isActive,
        long expectedVersion,
        CancellationToken cancellationToken = default)
    {
        User? user = await _userRepository.FindByIdAsync(id, cancellationToken);

        if (user is null)
        {
            return BaseResponse<UserDto>.NotFound(MessageCodes.Identity.UserNotFound, ("userId", id));
        }

        if (user.Version != expectedVersion)
        {
            return BaseResponse<UserDto>.ConcurrencyConflict();
        }

        var auditBefore = IdentityAuditFields.Capture(user);
        user.IsActive = isActive;
        user.RotateSecurityStamp();

        DateTime utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        await _unitOfWork.BeginAsync(cancellationToken);
        await _userRepository.UpdateAsync(user, cancellationToken);

        if (!isActive)
        {
            // Deactivation has to end the sessions too, otherwise the account keeps working
            // until its last refresh token expires.
            await _refreshTokenRepository.RevokeAllForUserAsync(
                user.Id,
                RefreshTokenRevocationReason.SecurityStampChanged,
                utcNow,
                cancellationToken);
        }

        await auditRecorder.RecordAsync(user.TenantId, user, ChangeKind.Updated,
            IdentityAuditFields.Diff(auditBefore, IdentityAuditFields.Capture(user))
                .Append(new FieldChange("SecurityStamp", "previous", "rotated")).ToArray(), cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        return BaseResponse<UserDto>.Success(ToDto(user));
    }

    private static UserDto ToDto(User user) =>
        new(
            user.Id,
            user.Email,
            user.DisplayName,
            user.OrgUnitId,
            [.. user.RoleAssignments.Select(assignment => new RoleAssignmentDto(
                assignment.RoleId,
                assignment.RoleCode,
                assignment.ReadScope,
                assignment.WriteScope))],
            user.IsActive,
            user.EmailConfirmed,
            user.MustChangePassword,
            user.LastLoginAtUtc,
            user.Version);
}

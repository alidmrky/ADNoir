using AdNoir.BuildingBlocks.Auditing;
using System.Globalization;
using AdNoir.Shared.Contracts.Events;
using AdNoir.BuildingBlocks.Configuration;
using AdNoir.BuildingBlocks.Context;
using AdNoir.Domain.Abstractions.Repositories;
using AdNoir.IdentityAndOrg.Application.Identity.Abstractions;
using AdNoir.IdentityAndOrg.Application.Identity.Dtos;
using AdNoir.IdentityAndOrg.Domain.Identity;
using AdNoir.IdentityAndOrg.Domain.OrgUnits;
using AdNoir.IdentityAndOrg.Domain.Tenants;
using AdNoir.Shared.Contracts.Messages;
using AdNoir.Shared.Contracts.Responses;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AdNoir.IdentityAndOrg.Application.Identity;

/// <summary>Default <see cref="IAuthService"/>.</summary>
public sealed class AuthService(
    ITenantRepository tenantRepository,
    IUserRepository userRepository,
    IOrgUnitRepository orgUnitRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IPasswordHasher passwordHasher,
    IAccessTokenIssuer tokenIssuer,
    ITenantContext tenantContext,
    ICurrentUser currentUser,
    IClientInfo clientInfo,
    ITransactionalAuditRecorder auditRecorder,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IOptions<JwtOptions> jwtOptions,
    IIntegrationEventPublisher eventPublisher,
    IOptions<RegistrationNotificationOptions> notificationOptions,
    ILogger<AuthService> logger) : IAuthService
{
    private readonly ITenantRepository _tenantRepository = tenantRepository;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IOrgUnitRepository _orgUnitRepository = orgUnitRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository = refreshTokenRepository;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;
    private readonly IAccessTokenIssuer _tokenIssuer = tokenIssuer;
    private readonly ITenantContext _tenantContext = tenantContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IClientInfo _clientInfo = clientInfo;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly JwtOptions _jwt = jwtOptions.Value;
    private readonly ILogger<AuthService> _logger = logger;

    /// <inheritdoc />
    public async Task<BaseResponse<UserDto>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Tenant? tenant = await ResolveTenantAsync(request.TenantSlug, cancellationToken);

        if (tenant is null)
        {
            return BaseResponse<UserDto>.NotFound(MessageCodes.Tenant.NotFound, ("tenantId", request.TenantSlug));
        }

        if (!tenant.IsUsable)
        {
            return BaseResponse<UserDto>.BusinessRuleViolation(MessageCodes.Tenant.Inactive);
        }

        string normalizedEmail = User.NormalizeEmail(request.Email);

        if (await _userRepository.EmailExistsAsync(normalizedEmail, excludingId: null, cancellationToken))
        {
            return BaseResponse<UserDto>.Conflict(MessageCodes.Identity.EmailConflict, ("email", request.Email));
        }

        IReadOnlyList<OrgUnit> roots = await _orgUnitRepository.ListRootsForRegistrationAsync(cancellationToken);
        OrgUnit? rootUnit = roots.Count > 0 ? roots[0] : null;

        if (rootUnit is null)
        {
            // A tenant with no root unit cannot place a user anywhere. The migrator creates one
            // during seeding, so this means the tenant was set up incompletely.
            return BaseResponse<UserDto>.BusinessRuleViolation(
                MessageCodes.OrgUnit.ParentNotFound,
                ("parentId", tenant.Id));
        }

        User user = User.Create(
            tenant.Id,
            rootUnit.Id,
            request.Email,
            request.DisplayName,
            _passwordHasher.Hash(request.Password));

        await _unitOfWork.BeginAsync(cancellationToken);
        await _userRepository.InsertAsync(user, cancellationToken);
        if (notificationOptions.Value.PublishWelcomeEvent)
        {
            await eventPublisher.PublishAsync(new UserRegisteredEvent
            {
                TenantId = tenant.Id,
                Email = user.Email,
                DisplayName = user.DisplayName,
                Culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName,
            }, cancellationToken);
        }
        await _unitOfWork.CommitAsync(cancellationToken);

        return BaseResponse<UserDto>.Created(ToDto(user));
    }

    /// <inheritdoc />
    public async Task<BaseResponse<AuthTokensDto>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        DateTime utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        Tenant? tenant = await ResolveTenantAsync(request.TenantSlug, cancellationToken);

        if (tenant is null || !tenant.IsUsable)
        {
            // Same answer as a wrong password: whether a tenant slug exists is not something an
            // unauthenticated caller needs to learn by probing.
            _passwordHasher.PerformDummyVerification();
            return InvalidCredentials();
        }

        User? user = await _userRepository.FindByNormalizedEmailAsync(
            User.NormalizeEmail(request.Email),
            cancellationToken);

        if (user is null)
        {
            // Spend the same time as a real verification would. Returning immediately here is
            // what turns a login endpoint into an account-enumeration oracle.
            _passwordHasher.PerformDummyVerification();
            return InvalidCredentials();
        }

        PasswordVerificationOutcome verification = _passwordHasher.Verify(user.PasswordHash, request.Password);
        bool passwordCorrect = verification != PasswordVerificationOutcome.Failed;

        if (!passwordCorrect)
        {
            await RegisterFailedAttemptAsync(user, utcNow, cancellationToken);
            return InvalidCredentials();
        }

        // Only a caller who proved they know the password learns that the account is locked or
        // deactivated. Someone guessing sees the generic message and learns nothing.
        if (user.IsLockedOut(utcNow))
        {
            return BaseResponse<AuthTokensDto>.Failure(
                ResponseStatus.Forbidden,
                MessageCodes.Identity.AccountLocked);
        }

        if (!user.IsActive)
        {
            return BaseResponse<AuthTokensDto>.Failure(
                ResponseStatus.Forbidden,
                MessageCodes.Identity.AccountInactive);
        }

        if (verification == PasswordVerificationOutcome.SuccessRehashNeeded)
        {
            // The plaintext is in hand exactly once per sign-in; upgrading the stored hash now
            // is the only moment it can be done without asking the user for anything.
            user.PasswordHash = _passwordHasher.Hash(request.Password);
        }

        user.AccessFailedCount = 0;
        user.LockoutEndUtc = null;
        user.LastLoginAtUtc = utcNow;

        string? hierarchyPath = await ResolveHierarchyPathAsync(user, cancellationToken);

        await _unitOfWork.BeginAsync(cancellationToken);

        await _userRepository.UpdateForAuthenticationAsync(user, cancellationToken);

        (string refreshToken, RefreshToken record) = StartRefreshFamily(user, utcNow);
        await _refreshTokenRepository.InsertAsync(record, cancellationToken);

        await _unitOfWork.CommitAsync(cancellationToken);

        IssuedAccessToken accessToken = _tokenIssuer.Issue(user, tenant, hierarchyPath);

        return BaseResponse<AuthTokensDto>.Success(new AuthTokensDto(
            accessToken.Token,
            (int)(accessToken.ExpiresAtUtc - utcNow).TotalSeconds,
            refreshToken,
            user.MustChangePassword));
    }

    /// <inheritdoc />
    public async Task<BaseResponse<AuthTokensDto>> RefreshAsync(
        RefreshRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        DateTime utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        string tokenHash = RefreshTokenGenerator.Hash(request.RefreshToken);

        RefreshToken? record = await _refreshTokenRepository.FindByHashAsync(tokenHash, cancellationToken);

        if (record is null)
        {
            return BaseResponse<AuthTokensDto>.Failure(
                ResponseStatus.Unauthorized,
                MessageCodes.Identity.RefreshTokenInvalid);
        }

        Tenant? tenant = await _tenantRepository.FindByIdAsync(record.TenantId, cancellationToken);

        if (tenant is null || !tenant.IsUsable)
        {
            return BaseResponse<AuthTokensDto>.Failure(
                ResponseStatus.Unauthorized,
                MessageCodes.Identity.RefreshTokenInvalid);
        }

        _tenantContext.SetTenant(tenant.Id, tenant.HierarchyEnabled);

        if (record.RevokedAtUtc is not null)
        {
            return await HandleReuseAsync(record, utcNow, cancellationToken);
        }

        if (record.ExpiresAtUtc <= utcNow)
        {
            return BaseResponse<AuthTokensDto>.Failure(
                ResponseStatus.Unauthorized,
                MessageCodes.Identity.RefreshTokenInvalid);
        }

        User? user = await _userRepository.FindByIdForAuthenticationAsync(record.UserId, cancellationToken);

        if (user is null || !user.IsActive || user.IsLockedOut(utcNow))
        {
            return BaseResponse<AuthTokensDto>.Failure(
                ResponseStatus.Unauthorized,
                MessageCodes.Identity.RefreshTokenInvalid);
        }

        string newToken = RefreshTokenGenerator.NewToken();
        string newHash = RefreshTokenGenerator.Hash(newToken);

        RefreshToken successor = record.CreateSuccessor(
            newHash,
            utcNow,
            TimeSpan.FromDays(_jwt.RefreshTokenDays),
            _clientInfo.IpAddress);

        record.Revoke(RefreshTokenRevocationReason.Rotated, utcNow, newHash);

        await _unitOfWork.BeginAsync(cancellationToken);
        await _refreshTokenRepository.UpdateAsync(record, cancellationToken);
        await _refreshTokenRepository.InsertAsync(successor, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        string? hierarchyPath = await ResolveHierarchyPathAsync(user, cancellationToken);

        // Claims are rebuilt from the user's current state, which is what makes a permission
        // change reach every service within one access-token lifetime.
        IssuedAccessToken accessToken = _tokenIssuer.Issue(user, tenant, hierarchyPath);

        return BaseResponse<AuthTokensDto>.Success(new AuthTokensDto(
            accessToken.Token,
            (int)(accessToken.ExpiresAtUtc - utcNow).TotalSeconds,
            newToken,
            user.MustChangePassword));
    }

    /// <inheritdoc />
    public async Task<BaseResponse> LogoutAsync(
        LogoutRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        DateTime utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        string tokenHash = RefreshTokenGenerator.Hash(request.RefreshToken);

        RefreshToken? record = await _refreshTokenRepository.FindByHashAsync(tokenHash, cancellationToken);

        // An unknown or already-revoked token still reports success. Sign-out is idempotent,
        // and reporting "no such token" would tell a caller which tokens are live.
        if (record is null)
        {
            return BaseResponse.NoContent();
        }

        Tenant? tenant = await _tenantRepository.FindByIdAsync(record.TenantId, cancellationToken);

        if (tenant is null)
        {
            return BaseResponse.NoContent();
        }

        _tenantContext.SetTenant(tenant.Id, tenant.HierarchyEnabled);

        await _unitOfWork.BeginAsync(cancellationToken);

        if (request.AllSessions)
        {
            await _refreshTokenRepository.RevokeAllForUserAsync(
                record.UserId,
                RefreshTokenRevocationReason.SignedOut,
                utcNow,
                cancellationToken);
        }
        else
        {
            await _refreshTokenRepository.RevokeFamilyAsync(
                record.FamilyId,
                RefreshTokenRevocationReason.SignedOut,
                utcNow,
                cancellationToken);
        }

        await _unitOfWork.CommitAsync(cancellationToken);

        return BaseResponse.NoContent();
    }

    /// <inheritdoc />
    public async Task<BaseResponse> ChangePasswordAsync(
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_currentUser.IsAuthenticated)
        {
            return BaseResponse.Unauthorized();
        }

        DateTime utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        User? user = await _userRepository.FindByIdForAuthenticationAsync(_currentUser.UserId, cancellationToken);

        if (user is null)
        {
            return BaseResponse.NotFound(MessageCodes.Identity.UserNotFound, ("userId", _currentUser.UserId));
        }

        if (_passwordHasher.Verify(user.PasswordHash, request.CurrentPassword) == PasswordVerificationOutcome.Failed)
        {
            return BaseResponse.Failure(ResponseStatus.Unauthorized, MessageCodes.Identity.CredentialsInvalid);
        }

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.MustChangePassword = false;

        // Rotating the stamp and revoking every session is the point of a password change: if
        // the old password was compromised, the sessions opened with it must not survive it.
        user.RotateSecurityStamp();

        await _unitOfWork.BeginAsync(cancellationToken);
        await _userRepository.UpdateForAuthenticationAsync(user, cancellationToken);
        await _refreshTokenRepository.RevokeAllForUserAsync(
            user.Id,
            RefreshTokenRevocationReason.SecurityStampChanged,
            utcNow,
            cancellationToken);
        await auditRecorder.RecordAsync(user.TenantId, user, ChangeKind.Updated,
            [new FieldChange("Password", "previous", "changed")], cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        return BaseResponse.NoContent();
    }

    /// <summary>
    /// Handles a revoked token being presented again: the signature of a stolen token.
    /// </summary>
    /// <remarks>
    /// The whole family is revoked and the user's security stamp is rotated, which invalidates
    /// every access token already issued. Rejecting only the replayed token would leave the
    /// thief's copy working, since they hold the successor.
    /// </remarks>
    private async Task<BaseResponse<AuthTokensDto>> HandleReuseAsync(
        RefreshToken record,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "Refresh token reuse detected for user {UserId} in tenant {TenantId} (family {FamilyId}, " +
            "sequence {Sequence}, revoked {RevokedAt} as {Reason}). Revoking the family and rotating " +
            "the security stamp. Caller IP {IpAddress}.",
            record.UserId,
            record.TenantId,
            record.FamilyId,
            record.SequenceNumber,
            record.RevokedAtUtc,
            record.RevocationReason,
            _clientInfo.IpAddress);

        User? user = await _userRepository.FindByIdForAuthenticationAsync(record.UserId, cancellationToken);

        await _unitOfWork.BeginAsync(cancellationToken);

        await _refreshTokenRepository.RevokeFamilyAsync(
            record.FamilyId,
            RefreshTokenRevocationReason.ReuseDetected,
            utcNow,
            cancellationToken);

        if (user is not null)
        {
            user.RotateSecurityStamp();
            await _userRepository.UpdateForAuthenticationAsync(user, cancellationToken);
        }

        await _unitOfWork.CommitAsync(cancellationToken);

        return BaseResponse<AuthTokensDto>.Failure(
            ResponseStatus.Unauthorized,
            MessageCodes.Identity.RefreshTokenReuseDetected);
    }

    private async Task RegisterFailedAttemptAsync(User user, DateTime utcNow, CancellationToken cancellationToken)
    {
        (int failedCount, DateTime? lockoutEnd) = LockoutPolicy.RegisterFailure(user.AccessFailedCount, utcNow);

        user.AccessFailedCount = failedCount;
        user.LockoutEndUtc = lockoutEnd ?? user.LockoutEndUtc;

        if (lockoutEnd is not null)
        {
            _logger.LogWarning(
                "Account {UserId} in tenant {TenantId} locked out until {LockoutEnd} after {FailedCount} " +
                "failed attempts. Caller IP {IpAddress}.",
                user.Id,
                user.TenantId,
                lockoutEnd,
                failedCount,
                _clientInfo.IpAddress);
        }

        await _unitOfWork.BeginAsync(cancellationToken);
        await _userRepository.UpdateForAuthenticationAsync(user, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);
    }

    private (string Token, RefreshToken Record) StartRefreshFamily(User user, DateTime utcNow)
    {
        string token = RefreshTokenGenerator.NewToken();

        RefreshToken record = RefreshToken.StartFamily(
            user.TenantId,
            user.Id,
            RefreshTokenGenerator.Hash(token),
            utcNow,
            TimeSpan.FromDays(_jwt.RefreshTokenDays),
            _clientInfo.IpAddress);

        return (token, record);
    }

    /// <summary>
    /// Resolves the tenant from its slug and installs it in the scope.
    /// </summary>
    /// <remarks>
    /// Sign-in is anonymous, so no middleware could have set the tenant: the slug arrives in
    /// the request body, which middleware cannot read without buffering it. Resolving here and
    /// setting the context means every repository call after this point is tenant-filtered
    /// normally, with no cross-tenant query anywhere in the flow.
    /// </remarks>
    private async Task<Tenant?> ResolveTenantAsync(string tenantSlug, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tenantSlug))
        {
            return null;
        }

        Tenant? tenant = await _tenantRepository.FindBySlugAsync(tenantSlug.Trim(), cancellationToken);

        if (tenant is not null)
        {
            _tenantContext.SetTenant(tenant.Id, tenant.HierarchyEnabled);
        }

        return tenant;
    }

    private async Task<string?> ResolveHierarchyPathAsync(User user, CancellationToken cancellationToken)
    {
        if (user.OrgUnitId == Guid.Empty)
        {
            return null;
        }

        OrgUnit? unit = await _orgUnitRepository.FindByIdForAuthenticationAsync(user.OrgUnitId, cancellationToken);

        return unit?.HierarchyPath;
    }

    private static BaseResponse<AuthTokensDto> InvalidCredentials() =>
        BaseResponse<AuthTokensDto>.Failure(
            ResponseStatus.Unauthorized,
            MessageCodes.Identity.CredentialsInvalid);

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

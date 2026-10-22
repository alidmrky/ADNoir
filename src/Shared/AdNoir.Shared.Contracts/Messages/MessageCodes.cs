namespace AdNoir.Shared.Contracts.Messages;

/// <summary>
/// The catalog of message codes. Every user-facing message is identified by one
/// of these constants; literal strings are never written at a call site, so a typo fails to
/// compile instead of reaching a screen.
/// </summary>
/// <remarks>
/// <para>
/// Codes are hierarchical strings in <c>{module}.{entity}.{state}</c> form. Numeric codes
/// are deliberately not used: they need a central registry, cannot be read in code and
/// collide easily, whereas string keys are self-documenting and greppable.
/// </para>
/// <para>
/// Every constant here must have an entry in every supported culture file under
/// <c>AdNoir.BuildingBlocks/Messages/Resources</c>. An architecture test fails the build
/// when a code has no entry, when an entry has no code, or when the two cultures disagree
/// about placeholder names.
/// </para>
/// <para>
/// Resist splitting codes finely. A catalog of 800 near-identical entries is worse than 80
/// good ones; a distinct code is warranted only when the user would take a different action.
/// </para>
/// </remarks>
public static class MessageCodes
{
    /// <summary>Gateway request protocol errors.</summary>
    public static class Gateway
    {
        /// <summary>The HTTP method is not supported for the route.</summary>
        public const string MethodNotAllowed = "gateway.request.method_not_allowed";

        /// <summary>The request body must use a supported media type.</summary>
        public const string UnsupportedMediaType = "gateway.request.unsupported_media_type";
    }
    /// <summary>Outcomes that are not specific to any module.</summary>
    public static class Common
    {
        /// <summary>The operation succeeded.</summary>
        public const string Success = "common.success";

        /// <summary>A resource was created.</summary>
        public const string Created = "common.created";

        /// <summary>The request was accepted for asynchronous processing.</summary>
        public const string Accepted = "common.accepted";

        /// <summary>The operation succeeded and produced no content.</summary>
        public const string NoContent = "common.no_content";

        /// <summary>The requested resource does not exist.</summary>
        public const string NotFound = "common.not_found";

        /// <summary>Input failed validation.</summary>
        public const string ValidationFailed = "common.validation_failed";

        /// <summary>The operation conflicts with the current state of the resource.</summary>
        public const string Conflict = "common.conflict";

        /// <summary>The caller is not authenticated.</summary>
        public const string Unauthorized = "common.unauthorized";

        /// <summary>The caller lacks the required permission.</summary>
        public const string Forbidden = "common.forbidden";

        /// <summary>A domain rule rejected the operation.</summary>
        public const string BusinessRuleViolation = "common.business_rule_violation";

        /// <summary>Another writer modified the entity first.</summary>
        public const string ConcurrencyConflict = "common.concurrency_conflict";

        /// <summary>The caller exceeded the allowed request rate.</summary>
        public const string RateLimited = "common.rate_limited";

        /// <summary>A downstream service could not be reached or failed.</summary>
        public const string ExternalServiceError = "common.external_service_error";

        /// <summary>An unhandled fault occurred.</summary>
        public const string UnexpectedError = "common.unexpected_error";
    }

    /// <summary>
    /// Input validation. FluentValidation rules reference these through
    /// <c>WithErrorCode(...)</c>; <c>WithMessage(...)</c> with literal text is forbidden.
    /// </summary>
    public static class Validation
    {
        /// <summary>A required value was missing. Placeholder: <c>field</c>.</summary>
        public const string Required = "validation.required";

        /// <summary>A value exceeded its maximum length. Placeholders: <c>field</c>, <c>maxLength</c>.</summary>
        public const string MaxLength = "validation.max_length";

        /// <summary>A value was shorter than its minimum length. Placeholders: <c>field</c>, <c>minLength</c>.</summary>
        public const string MinLength = "validation.min_length";

        /// <summary>A value did not match its expected format. Placeholder: <c>field</c>.</summary>
        public const string InvalidFormat = "validation.invalid_format";

        /// <summary>A value was not a valid e-mail address. Placeholder: <c>field</c>.</summary>
        public const string InvalidEmail = "validation.invalid_email";

        /// <summary>A value fell outside its allowed range. Placeholders: <c>field</c>, <c>min</c>, <c>max</c>.</summary>
        public const string OutOfRange = "validation.out_of_range";

        /// <summary>A value was not one of the allowed options. Placeholder: <c>field</c>.</summary>
        public const string NotAllowed = "validation.not_allowed";
    }

    /// <summary>Tenancy and hierarchy.</summary>
    public static class Tenant
    {
        /// <summary>The tenant could not be resolved from the request.</summary>
        public const string NotResolved = "tenant.not_resolved";

        /// <summary>The tenant does not exist. Placeholder: <c>tenantId</c>.</summary>
        public const string NotFound = "tenant.not_found";

        /// <summary>The tenant is suspended or otherwise not usable.</summary>
        public const string Inactive = "tenant.inactive";

        /// <summary>A hierarchy operation was attempted while hierarchy is disabled for the tenant.</summary>
        public const string HierarchyDisabled = "tenant.hierarchy.disabled";

        /// <summary>A hierarchy path exceeded its maximum length. Placeholder: <c>maxLength</c>.</summary>
        public const string HierarchyPathTooLong = "tenant.hierarchy.path_too_long";

        /// <summary>The requested slug is already used by another tenant. Placeholder: <c>slug</c>.</summary>
        public const string SlugConflict = "tenant.slug_conflict";
    }

    /// <summary>Organisation units and the hierarchy tree.</summary>
    public static class OrgUnit
    {
        /// <summary>The organisation unit does not exist. Placeholder: <c>orgUnitId</c>.</summary>
        public const string NotFound = "org.unit.not_found";

        /// <summary>The requested parent unit does not exist. Placeholder: <c>parentId</c>.</summary>
        public const string ParentNotFound = "org.unit.parent_not_found";

        /// <summary>A sibling already uses this slug. Placeholder: <c>slug</c>.</summary>
        public const string SlugConflict = "org.unit.slug_conflict";

        /// <summary>
        /// The move would put a unit inside its own subtree.
        /// Placeholders: <c>orgUnitId</c>, <c>targetParentId</c>.
        /// </summary>
        public const string CycleDetected = "org.unit.cycle_detected";

        /// <summary>The unit still has children and cannot be deleted. Placeholder: <c>childCount</c>.</summary>
        public const string HasChildren = "org.unit.has_children";
    }

    /// <summary>User accounts, roles and sign-in.</summary>
    public static class Identity
    {
        /// <summary>The user does not exist. Placeholder: <c>userId</c>.</summary>
        public const string UserNotFound = "identity.user.not_found";

        /// <summary>The e-mail address is already registered in this tenant. Placeholder: <c>email</c>.</summary>
        public const string EmailConflict = "identity.user.email_conflict";

        /// <summary>
        /// Sign-in failed. Deliberately the same message whether the account is unknown or the
        /// password is wrong, so the response cannot be used to enumerate accounts.
        /// </summary>
        public const string CredentialsInvalid = "identity.credentials_invalid";

        /// <summary>
        /// The account is temporarily locked. Returned only to a caller who supplied the
        /// correct password, so it reveals nothing to someone guessing.
        /// </summary>
        public const string AccountLocked = "identity.account_locked";

        /// <summary>The account has been deactivated.</summary>
        public const string AccountInactive = "identity.account_inactive";

        /// <summary>The password must be changed before anything else can be done.</summary>
        public const string PasswordChangeRequired = "identity.password.change_required";

        /// <summary>The refresh token is unknown, expired or already revoked.</summary>
        public const string RefreshTokenInvalid = "identity.refresh_token.invalid";

        /// <summary>
        /// A revoked refresh token was presented again. The whole family has been revoked and
        /// the user must sign in again.
        /// </summary>
        public const string RefreshTokenReuseDetected = "identity.refresh_token.reuse_detected";

        /// <summary>The role does not exist. Placeholder: <c>roleCode</c>.</summary>
        public const string RoleNotFound = "identity.role.not_found";

        /// <summary>The role code is already used in this tenant. Placeholder: <c>roleCode</c>.</summary>
        public const string RoleConflict = "identity.role.conflict";

        /// <summary>System roles cannot be modified or deleted.</summary>
        public const string SystemRoleImmutable = "identity.role.system_immutable";
    }

    /// <summary>Authentication and authorization.</summary>
    public static class Auth
    {
        /// <summary>The caller lacks a required permission. Placeholder: <c>permission</c>.</summary>
        public const string PermissionDenied = "auth.permission_denied";

        /// <summary>The presented token has expired.</summary>
        public const string TokenExpired = "auth.token_expired";

        /// <summary>The presented token is malformed or its signature does not verify.</summary>
        public const string TokenInvalid = "auth.token_invalid";

        /// <summary>
        /// The permission matrix could not be loaded from Identity. Authorization fails closed:
        /// permission is never assumed.
        /// </summary>
        public const string PermissionMatrixUnavailable = "auth.permission_matrix_unavailable";
    }

    /// <summary>Sample service — demonstration patterns only.</summary>
    /// <remarks>
    /// This class exists to demonstrate messaging patterns. The Sample service itself is
    /// intentionally thin and disposable; these codes travel with it.
    /// </remarks>
    public static class Sample
    {
        /// <summary>The sample item does not exist. Placeholder: <c>itemId</c>.</summary>
        public const string ItemNotFound = "sample.item.not_found";

        /// <summary>A sample item was created successfully.</summary>
        public const string ItemCreated = "sample.item.created";
    }
}

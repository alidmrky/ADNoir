using AdNoir.IdentityAndOrg.Application.Identity.Dtos;
using AdNoir.Shared.Contracts.Messages;
using FluentValidation;

namespace AdNoir.IdentityAndOrg.Application.Identity.Validators;

/// <summary>Password rules, shared by every request that accepts one.</summary>
internal static class PasswordRules
{
    /// <summary>Minimum password length.</summary>
    public const int MinimumLength = 6;

    /// <summary>Maximum accepted length, to bound hashing work.</summary>
    public const int MaximumLength = 256;

    /// <summary>Applies the password rules to a string property.</summary>
    /// <typeparam name="T">The request type.</typeparam>
    /// <param name="rule">The rule builder.</param>
    /// <returns>The same rule builder.</returns>
    /// <remarks>
    /// Length only, deliberately. Composition rules ("one digit, one symbol") push people
    /// toward predictable substitutions while ruling out long passphrases, which are stronger.
    /// The upper bound exists so a very long input cannot be used to make the hasher expensive.
    /// </remarks>
    public static IRuleBuilderOptions<T, string> ApplyPasswordRules<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .NotEmpty().WithErrorCode(MessageCodes.Validation.Required)
            .MinimumLength(MinimumLength).WithErrorCode(MessageCodes.Validation.MinLength)
            .MaximumLength(MaximumLength).WithErrorCode(MessageCodes.Validation.MaxLength);
}

/// <summary>Validates <see cref="RegisterRequest"/>.</summary>
public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    /// <summary>Initializes a new instance.</summary>
    public RegisterRequestValidator()
    {
        RuleFor(request => request.TenantSlug)
            .NotEmpty().WithErrorCode(MessageCodes.Validation.Required);

        RuleFor(request => request.Email)
            .NotEmpty().WithErrorCode(MessageCodes.Validation.Required)
            .EmailAddress().WithErrorCode(MessageCodes.Validation.InvalidEmail)
            .MaximumLength(256).WithErrorCode(MessageCodes.Validation.MaxLength);

        RuleFor(request => request.DisplayName)
            .NotEmpty().WithErrorCode(MessageCodes.Validation.Required)
            .MaximumLength(200).WithErrorCode(MessageCodes.Validation.MaxLength);

        RuleFor(request => request.Password).ApplyPasswordRules();
    }
}

/// <summary>Validates <see cref="LoginRequest"/>.</summary>
public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    /// <summary>Initializes a new instance.</summary>
    public LoginRequestValidator()
    {
        RuleFor(request => request.TenantSlug)
            .NotEmpty().WithErrorCode(MessageCodes.Validation.Required);

        RuleFor(request => request.Email)
            .NotEmpty().WithErrorCode(MessageCodes.Validation.Required);

        // Length rules are deliberately absent. Rejecting a short password here would tell a
        // caller that the stored one is longer, and it would lock out accounts whose password
        // predates a rule change.
        RuleFor(request => request.Password)
            .NotEmpty().WithErrorCode(MessageCodes.Validation.Required);
    }
}

/// <summary>Validates <see cref="RefreshRequest"/>.</summary>
public sealed class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    /// <summary>Initializes a new instance.</summary>
    public RefreshRequestValidator()
    {
        RuleFor(request => request.RefreshToken)
            .NotEmpty().WithErrorCode(MessageCodes.Validation.Required);
    }
}

/// <summary>Validates <see cref="LogoutRequest"/>.</summary>
public sealed class LogoutRequestValidator : AbstractValidator<LogoutRequest>
{
    /// <summary>Initializes a new instance.</summary>
    public LogoutRequestValidator()
    {
        RuleFor(request => request.RefreshToken)
            .NotEmpty().WithErrorCode(MessageCodes.Validation.Required);
    }
}

/// <summary>Validates <see cref="ChangePasswordRequest"/>.</summary>
public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    /// <summary>Initializes a new instance.</summary>
    public ChangePasswordRequestValidator()
    {
        RuleFor(request => request.CurrentPassword)
            .NotEmpty().WithErrorCode(MessageCodes.Validation.Required);

        RuleFor(request => request.NewPassword).ApplyPasswordRules();

        RuleFor(request => request.NewPassword)
            .NotEqual(request => request.CurrentPassword)
            .WithErrorCode(MessageCodes.Validation.NotAllowed);
    }
}

/// <summary>Validates <see cref="CreateUserRequest"/>.</summary>
public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    /// <summary>Initializes a new instance.</summary>
    public CreateUserRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty().WithErrorCode(MessageCodes.Validation.Required)
            .EmailAddress().WithErrorCode(MessageCodes.Validation.InvalidEmail)
            .MaximumLength(256).WithErrorCode(MessageCodes.Validation.MaxLength);

        RuleFor(request => request.DisplayName)
            .NotEmpty().WithErrorCode(MessageCodes.Validation.Required)
            .MaximumLength(200).WithErrorCode(MessageCodes.Validation.MaxLength);

        RuleFor(request => request.Password).ApplyPasswordRules();

        RuleFor(request => request.OrgUnitId)
            .NotEmpty().WithErrorCode(MessageCodes.Validation.Required);
    }
}

/// <summary>Validates <see cref="AssignRolesRequest"/>.</summary>
public sealed class AssignRolesRequestValidator : AbstractValidator<AssignRolesRequest>
{
    /// <summary>Initializes a new instance.</summary>
    public AssignRolesRequestValidator()
    {
        RuleForEach(request => request.Assignments).ChildRules(assignment =>
        {
            assignment.RuleFor(item => item.RoleId)
                .NotEmpty().WithErrorCode(MessageCodes.Validation.Required);

            assignment.RuleFor(item => item.ReadScope)
                .IsInEnum().WithErrorCode(MessageCodes.Validation.NotAllowed);

            assignment.RuleFor(item => item.WriteScope)
                .IsInEnum().WithErrorCode(MessageCodes.Validation.NotAllowed);
        });

        RuleFor(request => request.ExpectedVersion)
            .GreaterThanOrEqualTo(0).WithErrorCode(MessageCodes.Validation.OutOfRange);
    }
}

/// <summary>Validates <see cref="CreateRoleRequest"/>.</summary>
public sealed class CreateRoleRequestValidator : AbstractValidator<CreateRoleRequest>
{
    /// <summary>Initializes a new instance.</summary>
    public CreateRoleRequestValidator()
    {
        RuleFor(request => request.Code)
            .NotEmpty().WithErrorCode(MessageCodes.Validation.Required)
            .MaximumLength(64).WithErrorCode(MessageCodes.Validation.MaxLength);

        RuleFor(request => request.Name)
            .NotEmpty().WithErrorCode(MessageCodes.Validation.Required)
            .MaximumLength(200).WithErrorCode(MessageCodes.Validation.MaxLength);

        // Whether each permission exists is checked in the service against the catalog: it is
        // a lookup, not a shape rule, and the catalog is not a validation concern.
        RuleFor(request => request.Permissions)
            .NotNull().WithErrorCode(MessageCodes.Validation.Required);
    }
}

/// <summary>Validates <see cref="UpdateRoleRequest"/>.</summary>
public sealed class UpdateRoleRequestValidator : AbstractValidator<UpdateRoleRequest>
{
    /// <summary>Initializes a new instance.</summary>
    public UpdateRoleRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty().WithErrorCode(MessageCodes.Validation.Required)
            .MaximumLength(200).WithErrorCode(MessageCodes.Validation.MaxLength);

        RuleFor(request => request.Permissions)
            .NotNull().WithErrorCode(MessageCodes.Validation.Required);

        RuleFor(request => request.ExpectedVersion)
            .GreaterThanOrEqualTo(0).WithErrorCode(MessageCodes.Validation.OutOfRange);
    }
}

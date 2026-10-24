using AdNoir.IdentityAndOrg.Application.Identity.Dtos;
using AdNoir.Shared.Contracts.Messages;
using FluentValidation;

namespace AdNoir.IdentityAndOrg.Application.Identity.Validators;

/// <summary>Validates <see cref="ResetUserPasswordRequest"/>.</summary>
public sealed class ResetUserPasswordRequestValidator : AbstractValidator<ResetUserPasswordRequest>
{
    /// <summary>Initializes a new instance.</summary>
    public ResetUserPasswordRequestValidator()
    {
        RuleFor(request => request.NewPassword).ApplyPasswordRules();

        RuleFor(request => request.ExpectedVersion)
            .GreaterThanOrEqualTo(0).WithErrorCode(MessageCodes.Validation.OutOfRange);
    }
}

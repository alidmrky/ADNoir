using AdNoir.IdentityAndOrg.Application.OrgUnits.Dtos;
using AdNoir.IdentityAndOrg.Domain.Hierarchy;
using AdNoir.Shared.Contracts.Messages;
using FluentValidation;

namespace AdNoir.IdentityAndOrg.Application.OrgUnits.Validators;

/// <summary>Validates <see cref="CreateOrgUnitRequest"/>.</summary>
public sealed class CreateOrgUnitRequestValidator : AbstractValidator<CreateOrgUnitRequest>
{
    /// <summary>Initializes a new instance.</summary>
    public CreateOrgUnitRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty().WithErrorCode(MessageCodes.Validation.Required)
            .MaximumLength(200).WithErrorCode(MessageCodes.Validation.MaxLength);

        RuleFor(request => request.Slug!)
            .Must(Slug.IsValid).WithErrorCode(MessageCodes.Validation.InvalidFormat)
            .When(request => !string.IsNullOrWhiteSpace(request.Slug));

        RuleFor(request => request.Code!)
            .MaximumLength(64).WithErrorCode(MessageCodes.Validation.MaxLength)
            .When(request => request.Code is not null);
    }
}

/// <summary>Validates <see cref="RenameOrgUnitRequest"/>.</summary>
public sealed class RenameOrgUnitRequestValidator : AbstractValidator<RenameOrgUnitRequest>
{
    /// <summary>Initializes a new instance.</summary>
    public RenameOrgUnitRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty().WithErrorCode(MessageCodes.Validation.Required)
            .MaximumLength(200).WithErrorCode(MessageCodes.Validation.MaxLength);

        RuleFor(request => request.Slug!)
            .Must(Slug.IsValid).WithErrorCode(MessageCodes.Validation.InvalidFormat)
            .When(request => !string.IsNullOrWhiteSpace(request.Slug));

        RuleFor(request => request.Code!)
            .MaximumLength(64).WithErrorCode(MessageCodes.Validation.MaxLength)
            .When(request => request.Code is not null);

        RuleFor(request => request.ExpectedVersion)
            .GreaterThanOrEqualTo(0).WithErrorCode(MessageCodes.Validation.OutOfRange);
    }
}

/// <summary>Validates <see cref="MoveOrgUnitRequest"/>.</summary>
public sealed class MoveOrgUnitRequestValidator : AbstractValidator<MoveOrgUnitRequest>
{
    /// <summary>Initializes a new instance.</summary>
    public MoveOrgUnitRequestValidator()
    {
        RuleFor(request => request.ExpectedVersion)
            .GreaterThanOrEqualTo(0).WithErrorCode(MessageCodes.Validation.OutOfRange);

        // Cycle detection is not a validation rule: it needs the tree, so it lives in the
        // service where the nodes are already loaded.
    }
}

using AdNoir.IdentityAndOrg.Application.Tenants.Dtos;
using AdNoir.IdentityAndOrg.Domain.Hierarchy;
using AdNoir.Shared.Contracts.Messages;
using FluentValidation;

namespace AdNoir.IdentityAndOrg.Application.Tenants.Validators;

/// <summary>Validates <see cref="CreateTenantRequest"/>.</summary>
/// <remarks>
/// Rules carry catalog codes through <c>WithErrorCode</c> and never literal text through
/// <c>WithMessage</c> - the wording belongs to the message catalog, so it can be translated
/// and reviewed like every other user-facing string.
/// </remarks>
public sealed class CreateTenantRequestValidator : AbstractValidator<CreateTenantRequest>
{
    /// <summary>Initializes a new instance.</summary>
    public CreateTenantRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty().WithErrorCode(MessageCodes.Validation.Required)
            .MaximumLength(200).WithErrorCode(MessageCodes.Validation.MaxLength);

        RuleFor(request => request.Slug!)
            .Must(Slug.IsValid).WithErrorCode(MessageCodes.Validation.InvalidFormat)
            .When(request => !string.IsNullOrWhiteSpace(request.Slug));

        RuleFor(request => request.TimeZoneId)
            .NotEmpty().WithErrorCode(MessageCodes.Validation.Required)
            .MaximumLength(64).WithErrorCode(MessageCodes.Validation.MaxLength);
    }
}

/// <summary>Validates <see cref="UpdateTenantRequest"/>.</summary>
public sealed class UpdateTenantRequestValidator : AbstractValidator<UpdateTenantRequest>
{
    /// <summary>Initializes a new instance.</summary>
    public UpdateTenantRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty().WithErrorCode(MessageCodes.Validation.Required)
            .MaximumLength(200).WithErrorCode(MessageCodes.Validation.MaxLength);

        RuleFor(request => request.TimeZoneId)
            .NotEmpty().WithErrorCode(MessageCodes.Validation.Required)
            .MaximumLength(64).WithErrorCode(MessageCodes.Validation.MaxLength);

        RuleFor(request => request.Status)
            .IsInEnum().WithErrorCode(MessageCodes.Validation.NotAllowed);

        RuleFor(request => request.ExpectedVersion)
            .GreaterThanOrEqualTo(0).WithErrorCode(MessageCodes.Validation.OutOfRange);
    }
}

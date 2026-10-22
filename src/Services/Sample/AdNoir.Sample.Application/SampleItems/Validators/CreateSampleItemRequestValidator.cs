using AdNoir.Sample.Application.SampleItems.Dto;
using AdNoir.Shared.Contracts.Messages;
using FluentValidation;

namespace AdNoir.Sample.Application.SampleItems.Validators;

/// <summary>
/// Validates <see cref="CreateSampleItemRequest"/> inputs.
/// </summary>
/// <remarks>
/// FluentValidation rules use <c>WithErrorCode(MessageCodes.*)</c>; literal text through
/// <c>WithMessage</c> is forbidden (AGENTS.md §5).
/// </remarks>
public sealed class CreateSampleItemRequestValidator : AbstractValidator<CreateSampleItemRequest>
{
    /// <summary>Initialises the validation rules.</summary>
    public CreateSampleItemRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .WithErrorCode(MessageCodes.Validation.Required)
            .MaximumLength(200)
            .WithErrorCode(MessageCodes.Validation.MaxLength);

        RuleFor(x => x.Description)
            .MaximumLength(1000)
            .When(x => x.Description is not null)
            .WithErrorCode(MessageCodes.Validation.MaxLength);
    }
}

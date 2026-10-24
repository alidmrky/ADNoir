using AdNoir.IdentityAndOrg.Application.Identity.Dtos;
using AdNoir.IdentityAndOrg.Application.Identity.Validators;
using FluentValidation.Results;
using Shouldly;
using Xunit;

namespace AdNoir.UnitTests.Identity;

public sealed class PasswordRuleTests
{
    private readonly ResetUserPasswordRequestValidator _validator = new();

    [Theory]
    [InlineData("12345", false)]
    [InlineData("123456", true)]
    [InlineData("a-much-longer-passphrase", true)]
    public void Minimum_length_is_six_characters(string password, bool valid)
    {
        ValidationResult result = _validator.Validate(new ResetUserPasswordRequest(password, 0));

        result.IsValid.ShouldBe(valid);
    }
}

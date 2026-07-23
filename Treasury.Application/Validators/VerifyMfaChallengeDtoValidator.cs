using FluentValidation;
using Treasury.Application.DTOs.Auth;

namespace Treasury.Application.Validators;

public class VerifyMfaChallengeDtoValidator
    : AbstractValidator<VerifyMfaChallengeDto>
{
    public VerifyMfaChallengeDtoValidator()
    {
        RuleFor(item => item.ChallengeToken)
            .NotEmpty()
            .MaximumLength(512);

        RuleFor(item => item.Code)
            .NotEmpty()
            .Matches("^\\d{6}$")
            .WithMessage(
                "The authenticator code must contain " +
                "exactly six digits.");
    }
}

using FluentValidation;
using Treasury.Application.DTOs.Auth;

namespace Treasury.Application.Validators;

public class RegenerateMfaRecoveryCodesDtoValidator
    : AbstractValidator<
        RegenerateMfaRecoveryCodesDto>
{
    public RegenerateMfaRecoveryCodesDtoValidator()
    {
        RuleFor(item => item.CurrentPassword)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(item => item.Code)
            .NotEmpty()
            .Matches("^\\d{6}$")
            .WithMessage(
                "The authenticator code must contain " +
                "exactly six digits.");
    }
}

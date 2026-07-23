using FluentValidation;
using Treasury.Application.DTOs.Auth;

namespace Treasury.Application.Validators;

public class ConfirmMfaEnrollmentDtoValidator
    : AbstractValidator<ConfirmMfaEnrollmentDto>
{
    public ConfirmMfaEnrollmentDtoValidator()
    {
        RuleFor(item => item.Code)
            .NotEmpty()
            .Matches("^\\d{6}$")
            .WithMessage(
                "The authenticator code must contain " +
                "exactly six digits.");
    }
}

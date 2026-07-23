using FluentValidation;
using Treasury.Application.DTOs.Auth;

namespace Treasury.Application.Validators;

public class StartMfaEnrollmentDtoValidator
    : AbstractValidator<StartMfaEnrollmentDto>
{
    public StartMfaEnrollmentDtoValidator()
    {
        RuleFor(item => item.CurrentPassword)
            .NotEmpty()
            .MaximumLength(128);
    }
}

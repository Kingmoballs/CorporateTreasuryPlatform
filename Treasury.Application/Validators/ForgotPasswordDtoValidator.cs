using FluentValidation;
using Treasury.Application.DTOs.Auth;

namespace Treasury.Application.Validators;

public class ForgotPasswordDtoValidator
    : AbstractValidator<ForgotPasswordDto>
{
    public ForgotPasswordDtoValidator()
    {
        RuleFor(item => item.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);
    }
}

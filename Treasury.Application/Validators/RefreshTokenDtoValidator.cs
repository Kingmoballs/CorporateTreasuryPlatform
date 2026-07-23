using FluentValidation;
using Treasury.Application.DTOs.Auth;

namespace Treasury.Application.Validators;

public class RefreshTokenDtoValidator
    : AbstractValidator<RefreshTokenDto>
{
    public RefreshTokenDtoValidator()
    {
        RuleFor(item => item.RefreshToken)
            .NotEmpty()
            .MaximumLength(512);
    }
}

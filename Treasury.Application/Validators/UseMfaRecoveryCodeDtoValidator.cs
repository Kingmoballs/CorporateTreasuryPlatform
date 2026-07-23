using FluentValidation;
using Treasury.Application.DTOs.Auth;

namespace Treasury.Application.Validators;

public class UseMfaRecoveryCodeDtoValidator
    : AbstractValidator<UseMfaRecoveryCodeDto>
{
    public UseMfaRecoveryCodeDtoValidator()
    {
        RuleFor(item => item.ChallengeToken)
            .NotEmpty()
            .MaximumLength(512);

        RuleFor(item => item.RecoveryCode)
            .NotEmpty()
            .MaximumLength(64);
    }
}

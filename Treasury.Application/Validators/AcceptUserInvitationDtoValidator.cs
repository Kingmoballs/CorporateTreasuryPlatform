using FluentValidation;
using Treasury.Application.DTOs.Auth;

namespace Treasury.Application.Validators;

public class AcceptUserInvitationDtoValidator
    : AbstractValidator<AcceptUserInvitationDto>
{
    public AcceptUserInvitationDtoValidator()
    {
        RuleFor(item => item.Token)
            .NotEmpty()
            .MaximumLength(512);

        When(
            item =>
                !string.IsNullOrWhiteSpace(
                    item.Password),
            () =>
            {
                RuleFor(item => item.Password!)
                    .MinimumLength(12)
                    .MaximumLength(128)
                    .Matches("[A-Z]")
                    .WithMessage(
                        "Password must contain an " +
                        "uppercase letter.")
                    .Matches("[a-z]")
                    .WithMessage(
                        "Password must contain a " +
                        "lowercase letter.")
                    .Matches("[0-9]")
                    .WithMessage(
                        "Password must contain a number.")
                    .Matches("[^a-zA-Z0-9]")
                    .WithMessage(
                        "Password must contain a " +
                        "special character.");
            });
    }
}

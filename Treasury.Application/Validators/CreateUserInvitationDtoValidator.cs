using FluentValidation;
using Treasury.Application.DTOs.Admin;

namespace Treasury.Application.Validators;

public class CreateUserInvitationDtoValidator
    : AbstractValidator<CreateUserInvitationDto>
{
    public CreateUserInvitationDtoValidator()
    {
        RuleFor(item => item.FirstName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(item => item.LastName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(item => item.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);

        RuleFor(item => item.RoleId)
            .NotEmpty();
    }
}

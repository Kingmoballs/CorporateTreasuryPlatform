using FluentValidation;
using Treasury.Application.DTOs.Auth;

namespace Treasury.Application.Validators;

public class SwitchOrganizationDtoValidator
    : AbstractValidator<SwitchOrganizationDto>
{
    public SwitchOrganizationDtoValidator()
    {
        RuleFor(dto =>
                dto.OrganizationMembershipId)
            .NotEmpty();
    }
}

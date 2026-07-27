using FluentValidation;
using Treasury.Application.DTOs.OrganizationOnboarding;

namespace Treasury.Application.Validators;

public class RejectOrganizationApplicationDtoValidator
    : AbstractValidator<RejectOrganizationApplicationDto>
{
    public RejectOrganizationApplicationDtoValidator()
    {
        RuleFor(item => item.ConcurrencyToken)
            .NotEmpty();

        RuleFor(item => item.Reason)
            .NotEmpty()
            .MaximumLength(2000);
    }
}

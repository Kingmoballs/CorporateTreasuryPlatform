using FluentValidation;
using Treasury.Application.DTOs.OrganizationOnboarding;

namespace Treasury.Application.Validators;

public class ReviewOrganizationApplicationDtoValidator
    : AbstractValidator<ReviewOrganizationApplicationDto>
{
    public ReviewOrganizationApplicationDtoValidator()
    {
        RuleFor(item => item.ConcurrencyToken)
            .NotEmpty();
    }
}

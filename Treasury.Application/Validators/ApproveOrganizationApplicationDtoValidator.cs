using FluentValidation;
using Treasury.Application.DTOs.OrganizationOnboarding;

namespace Treasury.Application.Validators;

public class ApproveOrganizationApplicationDtoValidator
    : AbstractValidator<ApproveOrganizationApplicationDto>
{
    public ApproveOrganizationApplicationDtoValidator()
    {
        RuleFor(item => item.ConcurrencyToken)
            .NotEmpty();

        RuleFor(item => item.OrganizationCode)
            .NotEmpty()
            .MaximumLength(50)
            .Matches("^[A-Za-z0-9][A-Za-z0-9-]*$");

        RuleFor(item => item.OrganizationSlug)
            .NotEmpty()
            .MaximumLength(100)
            .Matches("^[a-z0-9][a-z0-9-]*$");

        RuleFor(item => item.LegalEntityCode)
            .NotEmpty()
            .MaximumLength(50)
            .Matches("^[A-Za-z0-9][A-Za-z0-9-]*$");

        RuleFor(item => item.LegalEntityName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(item => item.BusinessUnitCode)
            .NotEmpty()
            .MaximumLength(50)
            .Matches("^[A-Za-z0-9][A-Za-z0-9-]*$");

        RuleFor(item => item.BusinessUnitName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(item =>
                item.ApprovalThresholdAmount)
            .GreaterThanOrEqualTo(0);

        RuleFor(item =>
                item.RequiredApprovalCount)
            .InclusiveBetween(1, 5);

        RuleFor(item =>
                item.PendingRequestExpiryHours)
            .InclusiveBetween(1, 168);

        RuleFor(item => item.DecisionNotes)
            .MaximumLength(2000);
    }
}

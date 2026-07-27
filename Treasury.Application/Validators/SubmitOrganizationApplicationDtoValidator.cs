using FluentValidation;
using Treasury.Application.DTOs.OrganizationOnboarding;

namespace Treasury.Application.Validators;

public class SubmitOrganizationApplicationDtoValidator
    : AbstractValidator<SubmitOrganizationApplicationDto>
{
    public SubmitOrganizationApplicationDtoValidator()
    {
        RuleFor(item => item.OrganizationName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(item => item.RegistrationNumber)
            .MaximumLength(100);

        RuleFor(item => item.TaxIdentificationNumber)
            .MaximumLength(100);

        RuleFor(item => item.CountryCode)
            .NotEmpty()
            .Matches("^[A-Za-z]{2}$");

        RuleFor(item => item.BaseCurrency)
            .NotEmpty()
            .Matches("^[A-Za-z]{3}$");

        RuleFor(item => item.AdminFirstName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(item => item.AdminLastName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(item => item.AdminEmail)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);

        RuleFor(item => item.ContactPhoneNumber)
            .MaximumLength(30);

        RuleFor(item => item.ApplicationNotes)
            .MaximumLength(2000);
    }
}

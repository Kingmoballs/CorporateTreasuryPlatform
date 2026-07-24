using FluentValidation;
using Treasury.Application.DTOs.Organizations;

namespace Treasury.Application.Validators;

public class UpdateOrganizationProfileDtoValidator
    : AbstractValidator<
        UpdateOrganizationProfileDto>
{
    public UpdateOrganizationProfileDtoValidator()
    {
        RuleFor(dto => dto.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(dto => dto.CountryCode)
            .NotEmpty()
            .Matches("^[A-Za-z]{2}$");

        RuleFor(dto => dto.BaseCurrency)
            .NotEmpty()
            .Matches("^[A-Za-z]{3}$");

        RuleFor(dto => dto.ConcurrencyToken)
            .NotEmpty();
    }
}

public class CreateLegalEntityDtoValidator
    : AbstractValidator<CreateLegalEntityDto>
{
    public CreateLegalEntityDtoValidator()
    {
        RuleFor(dto => dto.Code)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(dto => dto.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(dto => dto.RegistrationNumber)
            .MaximumLength(100);

        RuleFor(dto =>
                dto.TaxIdentificationNumber)
            .MaximumLength(100);

        RuleFor(dto => dto.CountryCode)
            .NotEmpty()
            .Matches("^[A-Za-z]{2}$");

        RuleFor(dto => dto.BaseCurrency)
            .NotEmpty()
            .Matches("^[A-Za-z]{3}$");
    }
}

public class UpdateLegalEntityDtoValidator
    : AbstractValidator<UpdateLegalEntityDto>
{
    public UpdateLegalEntityDtoValidator()
    {
        RuleFor(dto => dto.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(dto => dto.RegistrationNumber)
            .MaximumLength(100);

        RuleFor(dto =>
                dto.TaxIdentificationNumber)
            .MaximumLength(100);

        RuleFor(dto => dto.CountryCode)
            .NotEmpty()
            .Matches("^[A-Za-z]{2}$");

        RuleFor(dto => dto.BaseCurrency)
            .NotEmpty()
            .Matches("^[A-Za-z]{3}$");

        RuleFor(dto => dto.ConcurrencyToken)
            .NotEmpty();
    }
}

public class CreateBusinessUnitDtoValidator
    : AbstractValidator<CreateBusinessUnitDto>
{
    public CreateBusinessUnitDtoValidator()
    {
        RuleFor(dto => dto.LegalEntityId)
            .NotEmpty();

        RuleFor(dto => dto.Code)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(dto => dto.Name)
            .NotEmpty()
            .MaximumLength(200);
    }
}

public class UpdateBusinessUnitDtoValidator
    : AbstractValidator<UpdateBusinessUnitDto>
{
    public UpdateBusinessUnitDtoValidator()
    {
        RuleFor(dto => dto.LegalEntityId)
            .NotEmpty();

        RuleFor(dto => dto.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(dto => dto.ConcurrencyToken)
            .NotEmpty();
    }
}

public class
    UpdateOrganizationStructureStatusDtoValidator
    : AbstractValidator<
        UpdateOrganizationStructureStatusDto>
{
    public
        UpdateOrganizationStructureStatusDtoValidator()
    {
        RuleFor(dto => dto.ConcurrencyToken)
            .NotEmpty();
    }
}

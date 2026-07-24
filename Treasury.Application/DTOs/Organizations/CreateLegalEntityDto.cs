namespace Treasury.Application.DTOs.Organizations;

public class CreateLegalEntityDto
{
    public string Code { get; set; } =
        string.Empty;

    public string Name { get; set; } =
        string.Empty;

    public string? RegistrationNumber
        { get; set; }

    public string? TaxIdentificationNumber
        { get; set; }

    public string CountryCode { get; set; } = "NG";

    public string BaseCurrency { get; set; } = "NGN";

    public bool IsActive { get; set; } = true;
}

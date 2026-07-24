namespace Treasury.Application.DTOs.Organizations;

public class UpdateLegalEntityDto
{
    public string Name { get; set; } =
        string.Empty;

    public string? RegistrationNumber
        { get; set; }

    public string? TaxIdentificationNumber
        { get; set; }

    public string CountryCode { get; set; } =
        string.Empty;

    public string BaseCurrency { get; set; } =
        string.Empty;

    public Guid ConcurrencyToken { get; set; }
}

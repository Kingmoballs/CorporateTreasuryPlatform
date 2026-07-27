namespace Treasury.Application.DTOs.OrganizationOnboarding;

public class SubmitOrganizationApplicationDto
{
    public string OrganizationName { get; set; } =
        string.Empty;

    public string? RegistrationNumber { get; set; }

    public string? TaxIdentificationNumber
        { get; set; }

    public string CountryCode { get; set; } =
        string.Empty;

    public string BaseCurrency { get; set; } =
        string.Empty;

    public string AdminFirstName { get; set; } =
        string.Empty;

    public string AdminLastName { get; set; } =
        string.Empty;

    public string AdminEmail { get; set; } =
        string.Empty;

    public string? ContactPhoneNumber { get; set; }

    public string? ApplicationNotes { get; set; }
}

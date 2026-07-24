namespace Treasury.Application.DTOs.Organizations;

public class UpdateOrganizationProfileDto
{
    public string Name { get; set; } =
        string.Empty;

    public string CountryCode { get; set; } =
        string.Empty;

    public string BaseCurrency { get; set; } =
        string.Empty;

    public Guid ConcurrencyToken { get; set; }
}

namespace Treasury.Application.DTOs.Organizations;

public class LegalEntityResponseDto
{
    public Guid Id { get; set; }

    public string Code { get; set; } =
        string.Empty;

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

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

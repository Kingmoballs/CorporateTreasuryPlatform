namespace Treasury.Application.DTOs.Organizations;

public class OrganizationProfileResponseDto
{
    public Guid Id { get; set; }

    public string Code { get; set; } =
        string.Empty;

    public string Name { get; set; } =
        string.Empty;

    public string Slug { get; set; } =
        string.Empty;

    public string CountryCode { get; set; } =
        string.Empty;

    public string BaseCurrency { get; set; } =
        string.Empty;

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

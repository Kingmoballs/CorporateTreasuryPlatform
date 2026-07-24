namespace Treasury.Application.DTOs.Organizations;

public class BusinessUnitResponseDto
{
    public Guid Id { get; set; }

    public Guid LegalEntityId { get; set; }

    public string LegalEntityCode { get; set; } =
        string.Empty;

    public string Code { get; set; } =
        string.Empty;

    public string Name { get; set; } =
        string.Empty;

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

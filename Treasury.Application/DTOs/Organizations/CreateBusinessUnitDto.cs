namespace Treasury.Application.DTOs.Organizations;

public class CreateBusinessUnitDto
{
    public Guid LegalEntityId { get; set; }

    public string Code { get; set; } =
        string.Empty;

    public string Name { get; set; } =
        string.Empty;

    public bool IsActive { get; set; } = true;
}

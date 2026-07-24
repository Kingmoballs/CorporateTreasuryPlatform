namespace Treasury.Application.DTOs.Organizations;

public class UpdateBusinessUnitDto
{
    public Guid LegalEntityId { get; set; }

    public string Name { get; set; } =
        string.Empty;

    public Guid ConcurrencyToken { get; set; }
}

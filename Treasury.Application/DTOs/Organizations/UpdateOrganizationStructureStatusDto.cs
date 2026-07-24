namespace Treasury.Application.DTOs.Organizations;

public class UpdateOrganizationStructureStatusDto
{
    public bool IsActive { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

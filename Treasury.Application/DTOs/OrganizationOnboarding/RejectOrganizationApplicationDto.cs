namespace Treasury.Application.DTOs.OrganizationOnboarding;

public class RejectOrganizationApplicationDto
{
    public Guid ConcurrencyToken { get; set; }

    public string Reason { get; set; } =
        string.Empty;
}

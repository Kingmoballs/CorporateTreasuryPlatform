namespace Treasury.Application.DTOs.Auth;

public class OrganizationAccessResponseDto
{
    public Guid OrganizationMembershipId
        { get; set; }

    public Guid OrganizationId { get; set; }

    public string OrganizationCode { get; set; } =
        string.Empty;

    public string OrganizationName { get; set; } =
        string.Empty;

    public string Role { get; set; } =
        string.Empty;

    public bool IsDefault { get; set; }

    public bool IsCurrent { get; set; }
}

namespace Treasury.Application.DTOs.Auth;

public class AcceptUserInvitationResponseDto
{
    public string Email { get; set; } = string.Empty;

    public Guid OrganizationId { get; set; }

    public string OrganizationCode { get; set; } =
        string.Empty;

    public string Role { get; set; } = string.Empty;

    public bool AccountCreated { get; set; }
}

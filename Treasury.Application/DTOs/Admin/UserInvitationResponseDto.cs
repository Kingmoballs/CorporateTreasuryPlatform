namespace Treasury.Application.DTOs.Admin;

public class UserInvitationResponseDto
{
    public Guid Id { get; set; }

    public string FirstName { get; set; } =
        string.Empty;

    public string LastName { get; set; } =
        string.Empty;

    public string Email { get; set; } =
        string.Empty;

    public Guid RoleId { get; set; }

    public string Role { get; set; } =
        string.Empty;

    public string Status { get; set; } =
        string.Empty;

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}

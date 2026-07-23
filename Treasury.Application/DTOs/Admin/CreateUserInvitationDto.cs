namespace Treasury.Application.DTOs.Admin;

public class CreateUserInvitationDto
{
    public string FirstName { get; set; } =
        string.Empty;

    public string LastName { get; set; } =
        string.Empty;

    public string Email { get; set; } =
        string.Empty;

    public Guid RoleId { get; set; }
}

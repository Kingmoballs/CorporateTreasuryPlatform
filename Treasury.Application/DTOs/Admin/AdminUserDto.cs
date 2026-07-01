namespace Treasury.Application.DTOs.Admin;

public class AdminUserDto
{
    public Guid Id { get; set; }

    public string FirstName { get; set; }
        = string.Empty;

    public string LastName { get; set; }
        = string.Empty;

    public string Email { get; set; }
        = string.Empty;

    public Guid RoleId { get; set; }

    public string Role { get; set; }
        = string.Empty;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }
}
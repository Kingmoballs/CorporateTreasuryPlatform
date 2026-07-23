public class CurrentUserDto
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public Guid? OrganizationId { get; set; }

    public string OrganizationCode { get; set; } =
        string.Empty;
}

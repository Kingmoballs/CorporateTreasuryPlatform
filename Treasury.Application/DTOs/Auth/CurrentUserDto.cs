namespace Treasury.Application.DTOs.Auth;

public class CurrentUserDto
{
    public Guid Id { get; set; }

    public string FirstName { get; set; } =
        string.Empty;

    public string LastName { get; set; } =
        string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public Guid? OrganizationId { get; set; }

    public Guid? OrganizationMembershipId
        { get; set; }

    public Guid? AuthenticationSessionId
        { get; set; }

    public string OrganizationCode { get; set; } =
        string.Empty;

    public bool MfaEnabled { get; set; }

    public DateTime? MfaEnabledAtUtc
        { get; set; }

    public DateTime? EmailVerifiedAtUtc
        { get; set; }

    public DateTime? PasswordChangedAtUtc
        { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}

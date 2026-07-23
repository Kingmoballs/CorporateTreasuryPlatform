namespace Treasury.Application.DTOs.Auth;

public class AuthenticationSessionResponseDto
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public string OrganizationCode { get; set; } =
        string.Empty;

    public string AuthenticationMethod
        { get; set; } = string.Empty;

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime LastActivityAtUtc
        { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public bool IsCurrent { get; set; }
}

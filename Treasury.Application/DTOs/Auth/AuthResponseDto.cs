namespace Treasury.Application.DTOs.Auth;

public class AuthResponseDto
{
    public bool MfaRequired { get; set; }

    public string? MfaChallengeToken
        { get; set; }

    public DateTime? MfaChallengeExpiresAtUtc
        { get; set; }

    public string AccessToken { get; set; } = string.Empty;

    public string RefreshToken { get; set; } =
        string.Empty;

    public DateTime AccessTokenExpiresAtUtc
        { get; set; }

    public DateTime RefreshTokenExpiresAtUtc
        { get; set; }

    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public Guid? OrganizationId { get; set; }

    public Guid? OrganizationMembershipId
        { get; set; }

    public string OrganizationCode { get; set; } =
        string.Empty;
}

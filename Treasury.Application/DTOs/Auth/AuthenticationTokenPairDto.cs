namespace Treasury.Application.DTOs.Auth;

public class AuthenticationTokenPairDto
{
    public string AccessToken { get; set; } =
        string.Empty;

    public string RefreshToken { get; set; } =
        string.Empty;

    public DateTime AccessTokenExpiresAtUtc
        { get; set; }

    public DateTime RefreshTokenExpiresAtUtc
        { get; set; }
}

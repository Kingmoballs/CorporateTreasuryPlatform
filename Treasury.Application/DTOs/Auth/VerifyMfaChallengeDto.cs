namespace Treasury.Application.DTOs.Auth;

public class VerifyMfaChallengeDto
{
    public string ChallengeToken { get; set; } =
        string.Empty;

    public string Code { get; set; } =
        string.Empty;
}

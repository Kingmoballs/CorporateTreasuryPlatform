namespace Treasury.Application.DTOs.Auth;

public class UseMfaRecoveryCodeDto
{
    public string ChallengeToken { get; set; } =
        string.Empty;

    public string RecoveryCode { get; set; } =
        string.Empty;
}

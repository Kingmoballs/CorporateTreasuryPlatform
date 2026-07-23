namespace Treasury.Infrastructure.Authentication;

public class MultiFactorAuthenticationOptions
{
    public const string SectionName =
        "MultiFactorAuthentication";

    public string Issuer { get; set; } =
        "Corporate Treasury Platform";

    public int EnrollmentMinutes { get; set; } =
        15;

    public int ChallengeMinutes { get; set; } =
        5;

    public int MaximumChallengeAttempts
        { get; set; } = 5;

    public int RecoveryCodeCount { get; set; } =
        10;
}

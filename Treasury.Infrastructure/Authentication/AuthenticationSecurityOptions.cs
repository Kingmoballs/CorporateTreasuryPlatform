namespace Treasury.Infrastructure.Authentication;

public class AuthenticationSecurityOptions
{
    public const string SectionName =
        "AuthenticationSecurity";

    public int MaximumFailedLoginAttempts
        { get; set; } = 5;

    public int LoginFailureWindowMinutes
        { get; set; } = 15;

    public int LoginLockoutMinutes
        { get; set; } = 15;

    public int LoginRequestsPerMinute
        { get; set; } = 10;

    public int RefreshRequestsPerMinute
        { get; set; } = 30;

    public int PasswordRecoveryRequestsPerMinute
        { get; set; } = 5;

    public int MfaVerificationRequestsPerMinute
        { get; set; } = 10;
}

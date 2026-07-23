namespace Treasury.Shared.Constants;

public static class AuthenticationSecurityEventTypes
{
    public const string LoginSucceeded =
        "login_succeeded";

    public const string LoginFailed = "login_failed";

    public const string LoginBlocked = "login_blocked";

    public const string SessionCreated =
        "session_created";

    public const string SessionRefreshed =
        "session_refreshed";

    public const string SessionRevoked =
        "session_revoked";

    public const string OrganizationSwitched =
        "organization_switched";

    public const string RefreshTokenReuse =
        "refresh_token_reuse";

    public const string PasswordChanged =
        "password_changed";

    public const string MfaChallengeFailed =
        "mfa_challenge_failed";

    public const string MfaRecoveryCodeUsed =
        "mfa_recovery_code_used";

    public const string MfaEnabled = "mfa_enabled";

    public const string MfaDisabled = "mfa_disabled";

    public const string MfaRecoveryCodesRegenerated =
        "mfa_recovery_codes_regenerated";
}

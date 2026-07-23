namespace Treasury.Infrastructure.Authentication;

public class PasswordRecoveryOptions
{
    public const string SectionName =
        "PasswordRecovery";

    public int TokenExpiryMinutes { get; set; } =
        30;

    public int RequestCooldownMinutes { get; set; } =
        5;

    public string ResetUrl { get; set; } =
        string.Empty;
}

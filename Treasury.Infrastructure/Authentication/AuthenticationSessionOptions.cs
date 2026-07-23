namespace Treasury.Infrastructure.Authentication;

public class AuthenticationSessionOptions
{
    public const string SectionName =
        "AuthenticationSessions";

    public int RefreshTokenDays { get; set; } = 7;
}

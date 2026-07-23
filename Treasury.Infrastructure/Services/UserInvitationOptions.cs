namespace Treasury.Infrastructure.Services;

public class UserInvitationOptions
{
    public const string SectionName =
        "UserInvitations";

    public int ExpiryHours { get; set; } = 24;

    public string AcceptanceUrl { get; set; } =
        string.Empty;
}

namespace Treasury.Infrastructure.Services;

public class OrganizationOnboardingOptions
{
    public const string SectionName =
        "OrganizationOnboarding";

    public int ApplicationsPerHour { get; set; } =
        5;

    /*
     * This must remain false in production. Development may
     * enable it to return the first-admin acceptance URL to
     * an authenticated PlatformAdmin when SMTP is disabled.
     */
    public bool
        ReturnManualInvitationUrlWhenEmailDisabled
        { get; set; }
}

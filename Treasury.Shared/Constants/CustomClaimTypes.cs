namespace Treasury.Shared.Constants;

/*
 * Custom JWT claim names used to carry the active
 * organization context through authenticated requests.
 */
public static class CustomClaimTypes
{
    public const string OrganizationId =
        "organization_id";

    public const string OrganizationCode =
        "organization_code";

    public const string OrganizationMembershipId =
        "organization_membership_id";

    public const string AuthenticationSessionId =
        "authentication_session_id";
}

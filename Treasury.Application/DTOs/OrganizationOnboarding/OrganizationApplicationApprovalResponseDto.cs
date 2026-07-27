namespace Treasury.Application.DTOs.OrganizationOnboarding;

public class OrganizationApplicationApprovalResponseDto
{
    public OrganizationApplicationResponseDto Application
        { get; set; } = null!;

    public Guid OrganizationId { get; set; }

    public Guid LegalEntityId { get; set; }

    public Guid BusinessUnitId { get; set; }

    public Guid AdminInvitationId { get; set; }

    public DateTime InvitationExpiresAtUtc
        { get; set; }

    public bool InvitationEmailSent { get; set; }

    /*
     * Populated only when development manual delivery is
     * explicitly enabled and SMTP delivery is disabled.
     */
    public string? ManualInvitationAcceptanceUrl
        { get; set; }
}

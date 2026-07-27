namespace Treasury.Application.DTOs.OrganizationOnboarding;

public class ApproveOrganizationApplicationDto
{
    public Guid ConcurrencyToken { get; set; }

    public string OrganizationCode { get; set; } =
        string.Empty;

    public string OrganizationSlug { get; set; } =
        string.Empty;

    public string LegalEntityCode { get; set; } =
        string.Empty;

    public string LegalEntityName { get; set; } =
        string.Empty;

    public string BusinessUnitCode { get; set; } =
        "HEAD-OFFICE";

    public string BusinessUnitName { get; set; } =
        "Head Office";

    public decimal ApprovalThresholdAmount
        { get; set; }

    public int RequiredApprovalCount { get; set; } =
        1;

    public int PendingRequestExpiryHours
        { get; set; } = 24;

    public string? DecisionNotes { get; set; }
}

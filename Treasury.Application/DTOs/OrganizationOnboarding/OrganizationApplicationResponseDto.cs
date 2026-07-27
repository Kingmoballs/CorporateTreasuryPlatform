namespace Treasury.Application.DTOs.OrganizationOnboarding;

public class OrganizationApplicationResponseDto
{
    public Guid Id { get; set; }

    public Guid SubmissionKey { get; set; }

    public string OrganizationName { get; set; } =
        string.Empty;

    public string? RegistrationNumber { get; set; }

    public string? TaxIdentificationNumber
        { get; set; }

    public string CountryCode { get; set; } =
        string.Empty;

    public string BaseCurrency { get; set; } =
        string.Empty;

    public string AdminFirstName { get; set; } =
        string.Empty;

    public string AdminLastName { get; set; } =
        string.Empty;

    public string AdminEmail { get; set; } =
        string.Empty;

    public string? ContactPhoneNumber { get; set; }

    public string? ApplicationNotes { get; set; }

    public string Status { get; set; } =
        string.Empty;

    public string? DecisionNotes { get; set; }

    public DateTime SubmittedAtUtc { get; set; }

    public DateTime? ReviewStartedAtUtc { get; set; }

    public DateTime? DecisionAtUtc { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    public Guid? ProvisionedOrganizationId
        { get; set; }

    public Guid? ProvisionedLegalEntityId
        { get; set; }

    public Guid? ProvisionedBusinessUnitId
        { get; set; }

    public Guid? AdminInvitationId { get; set; }

    public Guid ConcurrencyToken { get; set; }

    public bool IsIdempotentReplay { get; set; }
}

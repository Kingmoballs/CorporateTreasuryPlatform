namespace Treasury.Domain.Entities;

/*
 * A pre-tenant application submitted by a company that
 * wants to use the platform. It intentionally does not
 * implement IOrganizationOwnedEntity because no customer
 * organization exists until the application is approved.
 */
public class OrganizationApplication
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SubmissionKey { get; set; }

    public string OrganizationName { get; set; } =
        string.Empty;

    public string NormalizedOrganizationName
        { get; set; } = string.Empty;

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
        "Submitted";

    public string? DecisionNotes { get; set; }

    public DateTime SubmittedAtUtc { get; set; } =
        DateTime.UtcNow;

    public DateTime? ReviewStartedAtUtc { get; set; }

    public DateTime? DecisionAtUtc { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    public User? ReviewedByUser { get; set; }

    public Guid? ProvisionedOrganizationId
        { get; set; }

    public Organization? ProvisionedOrganization
        { get; set; }

    public Guid? ProvisionedLegalEntityId
        { get; set; }

    public LegalEntity? ProvisionedLegalEntity
        { get; set; }

    public Guid? ProvisionedBusinessUnitId
        { get; set; }

    public BusinessUnit? ProvisionedBusinessUnit
        { get; set; }

    public Guid? AdminInvitationId { get; set; }

    public UserInvitation? AdminInvitation { get; set; }

    public Guid ConcurrencyToken { get; set; } =
        Guid.NewGuid();
}

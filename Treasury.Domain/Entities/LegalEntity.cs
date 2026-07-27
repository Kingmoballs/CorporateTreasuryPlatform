namespace Treasury.Domain.Entities;

/*
 * A separately registered company within an organization.
 * Later treasury records can be assigned to this level
 * without changing the organization boundary.
 */
public class LegalEntity
    : IOrganizationOwnedEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrganizationId { get; set; }

    public Organization Organization { get; set; } =
        null!;

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? RegistrationNumber { get; set; }

    public string? TaxIdentificationNumber
        { get; set; }

    public string CountryCode { get; set; } = "NG";

    public string BaseCurrency { get; set; } = "NGN";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } =
        DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } =
        DateTime.UtcNow;

    public Guid ConcurrencyToken { get; set; } =
        Guid.NewGuid();

    public ICollection<BusinessUnit> BusinessUnits
        { get; set; } = new List<BusinessUnit>();

    public ICollection<Account> Accounts
        { get; set; } = new List<Account>();
}

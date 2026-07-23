namespace Treasury.Domain.Entities;

/*
 * The top-level tenant boundary. Every company that uses
 * the platform will have one Organization record.
 */
public class Organization
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string CountryCode { get; set; } = "NG";

    public string BaseCurrency { get; set; } = "NGN";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } =
        DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } =
        DateTime.UtcNow;

    public Guid ConcurrencyToken { get; set; } =
        Guid.NewGuid();

    public ICollection<LegalEntity> LegalEntities
        { get; set; } = new List<LegalEntity>();

    public ICollection<BusinessUnit> BusinessUnits
        { get; set; } = new List<BusinessUnit>();

    public ICollection<OrganizationMembership>
        Memberships { get; set; } =
            new List<OrganizationMembership>();
}

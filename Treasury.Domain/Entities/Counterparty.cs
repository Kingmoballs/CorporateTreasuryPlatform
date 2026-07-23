namespace Treasury.Domain.Entities;

public class Counterparty : IOrganizationOwnedEntity
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    /*
     * Short internal identifier such as:
     * GTBANK, ACCESS, CBN or FBN.
     */
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string CounterpartyType { get; set; } = "Bank";

    public string CountryCode { get; set; } = "NG";

    public string? SwiftCode { get; set; }

    public string? CreditRating { get; set; }

    public bool IsActive { get; set; } = true;

    public string? Notes { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public User? CreatedByUser { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    public User? UpdatedByUser { get; set; }

    public DateTime CreatedAtUtc { get; set; } =
        DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } =
        DateTime.UtcNow;

    public Guid ConcurrencyToken { get; set; } =
        Guid.NewGuid();

    public ICollection<InvestmentPlacement>
        InvestmentPlacements { get; set; } =
            new List<InvestmentPlacement>();

    public ICollection<InvestmentLimit>
        InvestmentLimits { get; set; } =
            new List<InvestmentLimit>();
    
    public ICollection<CreditFacility>
        CreditFacilities { get; set; } =
            new List<CreditFacility>();
}

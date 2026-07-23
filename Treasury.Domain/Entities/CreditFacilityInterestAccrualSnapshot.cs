namespace Treasury.Domain.Entities;

public class CreditFacilityInterestAccrualSnapshot
    : IOrganizationOwnedEntity
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid CreditFacilityId { get; set; }

    public CreditFacility CreditFacility { get; set; } =
        null!;

    /*
     * Normalized UTC date for which interest was accrued.
     * Only one snapshot can exist per facility per date.
     */
    public DateTime SnapshotDateUtc { get; set; }

    /*
     * Snapshot values preserve the historical facility
     * information even if the facility changes later.
     */
    public string FacilityReference { get; set; } =
        string.Empty;

    public string FacilityName { get; set; } =
        string.Empty;

    public string LenderName { get; set; } =
        string.Empty;

    public string Currency { get; set; } =
        string.Empty;

    public string FacilityStatus { get; set; } =
        string.Empty;

    public decimal OutstandingPrincipalAmount
        { get; set; }

    public decimal AnnualInterestRate { get; set; }

    public int DayCountBasis { get; set; }

    public int AccruedDays { get; set; } = 1;

    public decimal AccruedInterestBefore
        { get; set; }

    public decimal AccruedInterestAmount
        { get; set; }

    public decimal AccruedInterestAfter
        { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public User? CreatedByUser { get; set; }

    public DateTime CreatedAtUtc { get; set; } =
        DateTime.UtcNow;
}

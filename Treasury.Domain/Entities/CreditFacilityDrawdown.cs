namespace Treasury.Domain.Entities;

public class CreditFacilityDrawdown
    : IOrganizationOwnedEntity
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    /*
     * Internally generated reference such as:
     * DRW-20260722-AB12CD34
     */
    public string Reference { get; set; } =
        string.Empty;

    public Guid CreditFacilityId { get; set; }

    public CreditFacility CreditFacility { get; set; } =
        null!;

    public Guid SettlementAccountId { get; set; }

    public Account SettlementAccount { get; set; } =
        null!;

    public decimal Amount { get; set; }

    public string Currency { get; set; } =
        string.Empty;

    /*
     * These snapshots provide an immutable record
     * of the facility utilization before and after
     * this drawdown.
     */
    public decimal OutstandingPrincipalBefore
        { get; set; }

    public decimal OutstandingPrincipalAfter
        { get; set; }

    public string Status { get; set; } =
        "Completed";

    public string? ExternalReference { get; set; }

    /*
     * The client generates this once and reuses it
     * when retrying the same drawdown.
     */
    public string IdempotencyKey { get; set; } =
        string.Empty;

    public string Description { get; set; } =
        string.Empty;

    public Guid TreasuryTransactionId { get; set; }

    public TreasuryTransaction TreasuryTransaction
        { get; set; } = null!;

    public Guid InitiatedByUserId { get; set; }

    public User InitiatedByUser { get; set; } =
        null!;

    public DateTime DrawdownDateUtc { get; set; } =
        DateTime.UtcNow;

    public DateTime CreatedAtUtc { get; set; } =
        DateTime.UtcNow;
}

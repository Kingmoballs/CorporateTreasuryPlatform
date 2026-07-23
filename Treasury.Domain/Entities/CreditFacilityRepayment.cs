namespace Treasury.Domain.Entities;

public class CreditFacilityRepayment
    : IOrganizationOwnedEntity
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    /*
     * Internally generated reference such as:
     * RPM-20260723-AB12CD34
     */
    public string Reference { get; set; } =
        string.Empty;

    public Guid CreditFacilityId { get; set; }

    public CreditFacility CreditFacility { get; set; } =
        null!;

    public Guid SettlementAccountId { get; set; }

    public Account SettlementAccount { get; set; } =
        null!;

    /*
     * Total cash paid. It must equal the principal
     * component plus the interest component.
     */
    public decimal Amount { get; set; }

    public decimal PrincipalAmount { get; set; }

    public decimal InterestAmount { get; set; }

    public string Currency { get; set; } =
        string.Empty;

    /*
     * Immutable snapshots of the debt position before
     * and after this repayment.
     */
    public decimal OutstandingPrincipalBefore
        { get; set; }

    public decimal OutstandingPrincipalAfter
        { get; set; }

    public decimal AccruedInterestBefore
        { get; set; }

    public decimal AccruedInterestAfter
        { get; set; }

    public string Status { get; set; } =
        "Completed";

    public string? ExternalReference { get; set; }

    /*
     * Generate once and reuse when retrying the same
     * repayment request.
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

    public DateTime RepaymentDateUtc { get; set; } =
        DateTime.UtcNow;

    public DateTime CreatedAtUtc { get; set; } =
        DateTime.UtcNow;
}

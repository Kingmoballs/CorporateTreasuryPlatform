namespace Treasury.Domain.Entities;

public class InvestmentRolloverRequest
    : IOrganizationOwnedEntity
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    /*
     * Original placement being rolled over.
     */
    public Guid OriginalInvestmentPlacementId
        { get; set; }

    public InvestmentPlacement OriginalInvestmentPlacement
        { get; set; } = null!;

    public string OriginalInvestmentReference
        { get; set; } = string.Empty;

    public string OriginalInstitutionName
        { get; set; } = string.Empty;

    public string Currency { get; set; } = string.Empty;

    public DateTime OriginalMaturityDateUtc
        { get; set; }

    public decimal OriginalPrincipalAmount
        { get; set; }

    /*
     * These amounts are copied from the approved quote.
     * This prevents calculations from silently changing
     * while the request is waiting for approval.
     */
    public decimal GrossInterestAmount { get; set; }

    public decimal GrossMaturityAmount { get; set; }

    public decimal WithholdingTaxRatePercentage
        { get; set; }

    public decimal WithholdingTaxAmount { get; set; }

    public decimal NetInterestAmount { get; set; }

    public decimal NetMaturityProceeds { get; set; }

    public string RolloverOption { get; set; } =
        string.Empty;

    public decimal RolloverPrincipalAmount
        { get; set; }

    public decimal CashPayoutAmount { get; set; }

    /*
     * Required only when CashPayoutAmount is greater
     * than zero.
     */
    public Guid? CashPayoutAccountId { get; set; }

    public Account? CashPayoutAccount { get; set; }

    public string NewInvestmentType { get; set; } =
        string.Empty;

    public string NewInstitutionName { get; set; } =
        string.Empty;

    public decimal NewAnnualInterestRate { get; set; }

    public int NewDayCountBasis { get; set; }

    public DateTime NewStartDateUtc { get; set; }

    public DateTime NewMaturityDateUtc { get; set; }

    public int NewTenorDays { get; set; }

    public decimal NewExpectedInterestAmount
        { get; set; }

    public decimal NewExpectedMaturityAmount
        { get; set; }

    public string RequestIdempotencyKey { get; set; } =
        string.Empty;

    /*
     * Generated internally and used during atomic
     * execution. It is not supplied by the client.
     */
    public string ExecutionIdempotencyKey
        { get; set; } = string.Empty;

    public string? ExternalReference { get; set; }

    public string? Notes { get; set; }

    public string Status { get; set; } = "Pending";

    public int RequiredApprovalCount { get; set; }

    public int ApprovalCount { get; set; }

    public Guid RequestedByUserId { get; set; }

    public User RequestedByUser { get; set; } = null!;

    public DateTime RequestedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public Guid? RejectedByUserId { get; set; }

    public User? RejectedByUser { get; set; }

    public DateTime? RejectedAtUtc { get; set; }

    public string? RejectionReason { get; set; }

    /*
     * These fields will be populated during the atomic
     * rollover execution stage.
     */
    public Guid? NewInvestmentPlacementId { get; set; }

    public InvestmentPlacement? NewInvestmentPlacement
        { get; set; }

    public Guid? CashPayoutTreasuryTransactionId
        { get; set; }

    public TreasuryTransaction?
        CashPayoutTreasuryTransaction { get; set; }

    public Guid? ExecutedByUserId { get; set; }

    public User? ExecutedByUser { get; set; }

    public DateTime? ExecutedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; } =
        Guid.NewGuid();

    public List<InvestmentRolloverDecision> Decisions
        { get; set; } = new();
}

namespace Treasury.Domain.Entities;

public class InvestmentEarlyRedemptionRequest
{
    public Guid Id { get; set; }

    public Guid InvestmentPlacementId { get; set; }

    public InvestmentPlacement InvestmentPlacement
        { get; set; } = null!;

    public string InvestmentReference { get; set; } =
        string.Empty;

    public string InstitutionName { get; set; } =
        string.Empty;

    public Guid DestinationAccountId { get; set; }

    public Account DestinationAccount { get; set; } =
        null!;

    public string Currency { get; set; } = string.Empty;

    public DateTime ProposedRedemptionDateUtc
        { get; set; }

    public decimal PrincipalAmount { get; set; }

    public decimal GrossAccruedInterestAmount
        { get; set; }

    public decimal PenaltyRatePercentage { get; set; }

    public decimal PenaltyAmount { get; set; }

    public decimal InterestAfterPenaltyAmount
        { get; set; }

    public decimal WithholdingTaxRatePercentage
        { get; set; }

    public decimal WithholdingTaxAmount { get; set; }

    public decimal NetInterestAmount { get; set; }

    public decimal EstimatedRedemptionProceeds
        { get; set; }

    public decimal ExpectedProceedsShortfall
        { get; set; }

    public string RequestIdempotencyKey { get; set; } =
        string.Empty;

    /*
     * This internally generated key will be used during
     * cash execution. It is not supplied by the client.
     */
    public string ExecutionIdempotencyKey { get; set; } =
        string.Empty;

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

    public Guid? RedemptionTreasuryTransactionId
        { get; set; }

    public TreasuryTransaction?
        RedemptionTreasuryTransaction { get; set; }

    public DateTime? ExecutedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; } =
        Guid.NewGuid();

    public List<InvestmentEarlyRedemptionDecision>
        Decisions { get; set; } = new();
}
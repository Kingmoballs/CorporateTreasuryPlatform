namespace Treasury.Application.DTOs.InvestmentPlacements;

public class InvestmentEarlyRedemptionRequestResponseDto
{
    public Guid Id { get; set; }

    public Guid InvestmentPlacementId { get; set; }

    public string InvestmentReference { get; set; } =
        string.Empty;

    public string InstitutionName { get; set; } =
        string.Empty;

    public Guid DestinationAccountId { get; set; }

    public string? DestinationAccountName { get; set; }

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

    public string ExecutionIdempotencyKey { get; set; } =
        string.Empty;

    public string? ExternalReference { get; set; }

    public string? Notes { get; set; }

    public string Status { get; set; } = string.Empty;

    public int RequiredApprovalCount { get; set; }

    public int ApprovalCount { get; set; }

    public int RemainingApprovalCount { get; set; }

    public Guid RequestedByUserId { get; set; }

    public DateTime RequestedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public Guid? RejectedByUserId { get; set; }

    public DateTime? RejectedAtUtc { get; set; }

    public string? RejectionReason { get; set; }

    public Guid? RedemptionTreasuryTransactionId
        { get; set; }

    public DateTime? ExecutedAtUtc { get; set; }

    public List<InvestmentEarlyRedemptionDecisionDto>
        Decisions { get; set; } = new();
}
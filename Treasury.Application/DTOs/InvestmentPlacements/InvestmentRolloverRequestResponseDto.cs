namespace Treasury.Application.DTOs.InvestmentPlacements;

public class InvestmentRolloverRequestResponseDto
{
    public Guid Id { get; set; }

    public Guid OriginalInvestmentPlacementId
        { get; set; }

    public string OriginalInvestmentReference
        { get; set; } = string.Empty;

    public string OriginalInstitutionName
        { get; set; } = string.Empty;

    public string Currency { get; set; } = string.Empty;

    public DateTime OriginalMaturityDateUtc
        { get; set; }

    public decimal OriginalPrincipalAmount
        { get; set; }

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

    public Guid? CashPayoutAccountId { get; set; }

    public string? CashPayoutAccountName { get; set; }

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

    public string RequestIdempotencyKey
        { get; set; } = string.Empty;

    public string ExecutionIdempotencyKey
        { get; set; } = string.Empty;

    public string? ExternalReference { get; set; }

    public string? Notes { get; set; }

    public string Status { get; set; } =
        string.Empty;

    public int RequiredApprovalCount { get; set; }

    public int ApprovalCount { get; set; }

    public int RemainingApprovalCount { get; set; }

    public Guid RequestedByUserId { get; set; }

    public DateTime RequestedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public Guid? RejectedByUserId { get; set; }

    public DateTime? RejectedAtUtc { get; set; }

    public string? RejectionReason { get; set; }

    public Guid? NewInvestmentPlacementId
        { get; set; }

    public Guid? CashPayoutTreasuryTransactionId
        { get; set; }

    public Guid? ExecutedByUserId { get; set; }

    public DateTime? ExecutedAtUtc { get; set; }

    public List<InvestmentRolloverDecisionDto>
        Decisions { get; set; } = new();
}
namespace Treasury.Application.DTOs.InvestmentPlacements;

public class InvestmentPlacementResponseDto
{
    public Guid Id { get; set; }

    public string Reference { get; set; } = string.Empty;

    public string InvestmentType { get; set; } = string.Empty;

    public string InstitutionName { get; set; } = string.Empty;

    public Guid? CounterpartyId { get; set; }

    public string? CounterpartyCode { get; set; }

    public string? CounterpartyName { get; set; }

    public Guid SourceAccountId { get; set; }

    public string SourceAccountName { get; set; } = string.Empty;

    public Guid? LegalEntityId { get; set; }

    public Guid? BusinessUnitId { get; set; }

    public decimal PrincipalAmount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public decimal AnnualInterestRate { get; set; }

    public int DayCountBasis { get; set; }

    public DateTime StartDateUtc { get; set; }

    public DateTime MaturityDateUtc { get; set; }

    public int TenorDays { get; set; }

    public decimal ExpectedInterestAmount { get; set; }

    public decimal ExpectedMaturityAmount { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? ExternalReference { get; set; }

    public string? Notes { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public int RequiredApprovalCount { get; set; }

    public int ApprovalCount { get; set; }

    public Guid? ActivationRequestedByUserId { get; set; }

    public DateTime? ActivationRequestedAtUtc { get; set; }

    public DateTime? ActivationExpiresAtUtc { get; set; }

    public Guid? ActivationRejectedByUserId { get; set; }

    public DateTime? ActivationRejectedAtUtc { get; set; }

    public string? ActivationRejectionReason { get; set; }

    public string? ActivationIdempotencyKey { get; set; }

    public Guid? FundingTreasuryTransactionId { get; set; }

    public string? FundingTransactionReference { get; set; }

    public Guid? MaturityForecastItemId { get; set; }

    public Guid? ActivatedByUserId { get; set; }

    public DateTime? ActivatedAtUtc { get; set; }

    public string? RedemptionIdempotencyKey { get; set; }

    public Guid? RedemptionAccountId { get; set; }

    public string? RedemptionAccountName { get; set; }

    public Guid? RedemptionTreasuryTransactionId
        { get; set; }

    public string? RedemptionTransactionReference
        { get; set; }

    public decimal ActualInterestAmount { get; set; }

    public decimal WithholdingTaxAmount { get; set; }

    public decimal ActualMaturityAmount { get; set; }

    public string? RedemptionExternalReference
        { get; set; }

    public string? RedemptionNotes { get; set; }

    public Guid? RedeemedByUserId { get; set; }

    public DateTime? RedeemedAtUtc { get; set; }

    public Guid? CancelledByUserId { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public string? CancellationReason { get; set; }
}

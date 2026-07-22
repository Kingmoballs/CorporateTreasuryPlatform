namespace Treasury.Application.DTOs.CreditFacilities;

public class CreditFacilityResponseDto
{
    public Guid Id { get; set; }

    public string Reference { get; set; } =
        string.Empty;

    public string FacilityName { get; set; } =
        string.Empty;

    public string FacilityType { get; set; } =
        string.Empty;

    public Guid LenderCounterpartyId { get; set; }

    public string LenderCode { get; set; } =
        string.Empty;

    public string LenderName { get; set; } =
        string.Empty;

    public Guid SettlementAccountId { get; set; }

    public string SettlementAccountName { get; set; } =
        string.Empty;

    public string SettlementAccountNumber { get; set; } =
        string.Empty;

    public string Currency { get; set; } =
        string.Empty;

    public decimal ApprovedLimitAmount { get; set; }

    public decimal OutstandingPrincipalAmount
        { get; set; }

    public decimal AccruedInterestAmount
        { get; set; }

    public decimal AvailableAmount { get; set; }

    public decimal TotalOutstandingAmount { get; set; }

    public decimal AnnualInterestRate { get; set; }

    public decimal CommitmentFeeRatePercentage
        { get; set; }

    public decimal ArrangementFeeAmount { get; set; }

    public int DayCountBasis { get; set; }

    public string InterestPaymentFrequency
        { get; set; } = string.Empty;

    public DateTime StartDateUtc { get; set; }

    public DateTime MaturityDateUtc { get; set; }

    public int TenorDays { get; set; }

    public string Status { get; set; } =
        string.Empty;

    public string? ExternalReference { get; set; }

    public string? Notes { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public int RequiredApprovalCount { get; set; }

    public int ApprovalCount { get; set; }

    public Guid? ActivationRequestedByUserId
        { get; set; }

    public DateTime? ActivationRequestedAtUtc
        { get; set; }

    public DateTime? ActivationExpiresAtUtc
        { get; set; }

    public string? ActivationIdempotencyKey
        { get; set; }

    public Guid? ActivationRejectedByUserId
        { get; set; }

    public DateTime? ActivationRejectedAtUtc
        { get; set; }

    public string? ActivationRejectionReason
        { get; set; }

    public Guid? ActivatedByUserId { get; set; }

    public DateTime? ActivatedAtUtc { get; set; }

    public Guid? SuspendedByUserId { get; set; }

    public DateTime? SuspendedAtUtc { get; set; }

    public string? SuspensionReason { get; set; }

    public DateTime? MaturedAtUtc { get; set; }

    public Guid? ClosedByUserId { get; set; }

    public DateTime? ClosedAtUtc { get; set; }

    public string? ClosureReason { get; set; }

    public Guid? CancelledByUserId { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public string? CancellationReason { get; set; }
}
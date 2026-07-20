namespace Treasury.Domain.Entities;

public class InvestmentPlacement
{
    public Guid Id { get; set; }

    public string Reference { get; set; } = string.Empty;

    public string InvestmentType { get; set; } = "FixedDeposit";

    public string InstitutionName { get; set; } = string.Empty;

    /*
    * Nullable temporarily so existing investment records
    * can migrate without losing data.
    */
    public Guid? CounterpartyId { get; set; }

    public Counterparty? Counterparty { get; set; }

    public Guid SourceAccountId { get; set; }

    public Account SourceAccount { get; set; } = null!;

    public decimal PrincipalAmount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public decimal AnnualInterestRate { get; set; }

    public int DayCountBasis { get; set; } = 365;

    public DateTime StartDateUtc { get; set; }

    public DateTime MaturityDateUtc { get; set; }

    public decimal ExpectedInterestAmount { get; set; }

    public decimal ExpectedMaturityAmount { get; set; }

    public string Status { get; set; } = "Draft";

    public string? ExternalReference { get; set; }

    public string? Notes { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public User? CreatedByUser { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public int RequiredApprovalCount { get; set; }

    public int ApprovalCount { get; set; }

    public Guid? ActivationRequestedByUserId { get; set; }

    public User? ActivationRequestedByUser { get; set; }

    public DateTime? ActivationRequestedAtUtc { get; set; }

    public DateTime? ActivationExpiresAtUtc { get; set; }

    public Guid? ActivationRejectedByUserId { get; set; }

    public User? ActivationRejectedByUser { get; set; }

    public DateTime? ActivationRejectedAtUtc { get; set; }

    public string? ActivationRejectionReason { get; set; }

    public string? ActivationIdempotencyKey { get; set; }

    public Guid? FundingTreasuryTransactionId { get; set; }

    public TreasuryTransaction?
        FundingTreasuryTransaction { get; set; }

    public Guid? MaturityForecastItemId { get; set; }

    public CashFlowForecastItem?
        MaturityForecastItem { get; set; }

    public Guid? ActivatedByUserId { get; set; }

    public User? ActivatedByUser { get; set; }

    public DateTime? ActivatedAtUtc { get; set; }

    public string? RedemptionIdempotencyKey { get; set; }

    public Guid? RedemptionAccountId { get; set; }

    public Account? RedemptionAccount { get; set; }

    public Guid? RedemptionTreasuryTransactionId
        { get; set; }

    public TreasuryTransaction?
        RedemptionTreasuryTransaction { get; set; }

    public decimal ActualInterestAmount { get; set; }

    public decimal WithholdingTaxAmount { get; set; }

    public decimal ActualMaturityAmount { get; set; }

    public string? RedemptionExternalReference
        { get; set; }

    public string? RedemptionNotes { get; set; }

    public Guid? RedeemedByUserId { get; set; }

    public User? RedeemedByUser { get; set; }

    public DateTime? RedeemedAtUtc { get; set; }

    public Guid? CancelledByUserId { get; set; }

    public User? CancelledByUser { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public string? CancellationReason { get; set; }

    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();
}
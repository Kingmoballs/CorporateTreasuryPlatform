namespace Treasury.Application.DTOs.CreditFacilityRepayments;

public class CreditFacilityRepaymentResponseDto
{
    public Guid Id { get; set; }

    public string Reference { get; set; } =
        string.Empty;

    public Guid CreditFacilityId { get; set; }

    public string CreditFacilityReference { get; set; } =
        string.Empty;

    public string FacilityName { get; set; } =
        string.Empty;

    public string LenderName { get; set; } =
        string.Empty;

    public Guid SettlementAccountId { get; set; }

    public string SettlementAccountName { get; set; } =
        string.Empty;

    public string SettlementAccountNumber { get; set; } =
        string.Empty;

    public decimal Amount { get; set; }

    public decimal PrincipalAmount { get; set; }

    public decimal InterestAmount { get; set; }

    public string Currency { get; set; } =
        string.Empty;

    public decimal OutstandingPrincipalBefore
        { get; set; }

    public decimal OutstandingPrincipalAfter
        { get; set; }

    public decimal AccruedInterestBefore
        { get; set; }

    public decimal AccruedInterestAfter
        { get; set; }

    public decimal TotalOutstandingAfter { get; set; }

    public string Status { get; set; } =
        string.Empty;

    public string? ExternalReference { get; set; }

    public string IdempotencyKey { get; set; } =
        string.Empty;

    public string Description { get; set; } =
        string.Empty;

    public Guid TreasuryTransactionId { get; set; }

    public string TreasuryTransactionReference
        { get; set; } = string.Empty;

    public Guid InitiatedByUserId { get; set; }

    public DateTime RepaymentDateUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
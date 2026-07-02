namespace Treasury.Application.DTOs.CashMovements;

public class CashPaymentResponseDto
{
    public Guid? PaymentRequestId { get; set; }

    public Guid? TransactionId { get; set; }

    public string? TransactionReference { get; set; }

    public string Status { get; set; }
        = string.Empty;

    public Guid AccountId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; }
        = string.Empty;

    public string BeneficiaryName { get; set; }
        = string.Empty;

    public string Category { get; set; }
        = string.Empty;

    public string? ExternalReference { get; set; }

    public string Description { get; set; }
        = string.Empty;

    public string? RejectionReason { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
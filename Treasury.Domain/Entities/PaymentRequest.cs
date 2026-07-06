namespace Treasury.Domain.Entities;

public class PaymentRequest
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; }
        = string.Empty;

    public string BeneficiaryName { get; set; }
        = string.Empty;

    public string Category { get; set; }
        = string.Empty;

    public string? ExternalReference { get; set; }

    public string IdempotencyKey { get; set; }
        = string.Empty;

    public string Description { get; set; }
        = string.Empty;

    public string Status { get; set; }
        = "Pending";

    public Guid RequestedByUserId { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    public DateTime? ReviewedAtUtc { get; set; }

    public string? RejectionReason { get; set; }

    public int RequiredApprovalCount { get; set; }
        = 1;

    public int ApprovalCount { get; set; }

    public Guid ConcurrencyToken { get; set; }
        = Guid.NewGuid();

    public DateTime CreatedAtUtc { get; set; }
        = DateTime.UtcNow;
}
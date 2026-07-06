namespace Treasury.Domain.Entities;

public class TransferRequest
{
    public Guid Id { get; set; }

    public Guid FromAccountId { get; set; }

    public Guid ToAccountId { get; set; }

    public decimal Amount { get; set; }

    public string Description { get; set; }
        = string.Empty;

    public string Status { get; set; }
        = "Pending";

    // Nullable so existing transfer requests can migrate safely.
    public Guid? RequestedByUserId { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    public DateTime? ReviewedAtUtc { get; set; }

    public string? RejectionReason { get; set; }

    public int RequiredApprovalCount { get; set; }
        = 1;

    public int ApprovalCount { get; set; }

    public DateTime? ExpiresAtUtc { get; set; }

    /*
     * Rotated whenever the request is processed.
     * EF uses it to detect simultaneous approvals.
     */
    public Guid ConcurrencyToken { get; set; }
        = Guid.NewGuid();

    public DateTime CreatedAt { get; set; }
        = DateTime.UtcNow;
}
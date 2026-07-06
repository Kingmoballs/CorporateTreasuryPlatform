namespace Treasury.Application.DTOs.Reversals;

public class ReversalRequestResponseDto
{
    public Guid Id { get; set; }

    public Guid OriginalTransactionId { get; set; }

    public string OriginalTransactionReference
    {
        get;
        set;
    } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; }
        = string.Empty;

    public string Reason { get; set; }
        = string.Empty;

    public string Status { get; set; }
        = string.Empty;

    public Guid RequestedByUserId { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    public DateTime? ReviewedAtUtc { get; set; }

    public string? RejectionReason { get; set; }

    public Guid? ReversalTransactionId { get; set; }

    public string? ReversalTransactionReference
    {
        get;
        set;
    }

    public int ApprovalCount { get; set; }

    public int RequiredApprovalCount { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? ExpiresAtUtc { get; set; }
}
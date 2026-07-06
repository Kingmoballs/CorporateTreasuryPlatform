namespace Treasury.Domain.Entities;

public class ApprovalDecision
{
    public Guid Id { get; set; }

    public Guid? TransferRequestId { get; set; }

    public Guid? PaymentRequestId { get; set; }

    public Guid? ReversalRequestId { get; set; }

    public Guid ApproverUserId { get; set; }

    public string Decision { get; set; }
        = string.Empty;

    public string? Comment { get; set; }

    public DateTime CreatedAtUtc { get; set; }
        = DateTime.UtcNow;
}
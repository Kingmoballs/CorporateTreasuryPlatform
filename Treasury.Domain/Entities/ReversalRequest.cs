namespace Treasury.Domain.Entities;

public class ReversalRequest
    : IOrganizationOwnedEntity
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid OriginalTransactionId { get; set; }

    public TreasuryTransaction
        OriginalTransaction { get; set; }
        = null!;

    public string Reason { get; set; }
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

    public DateTime? ExpiresAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
        = Guid.NewGuid();

    public DateTime CreatedAtUtc { get; set; }
        = DateTime.UtcNow;
}

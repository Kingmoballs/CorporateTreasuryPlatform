namespace Treasury.Domain.Entities;

public class ApprovalDecision : IOrganizationOwnedEntity
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid? TransferRequestId { get; set; }

    public Guid? PaymentRequestId { get; set; }

    public Guid? ReversalRequestId { get; set; }

    public Guid? InvestmentPlacementId { get; set; }

    public Guid? CreditFacilityId { get; set; }

    public Guid ApproverUserId { get; set; }

    public User Approver { get; set; }
        = null!;

    public string Decision { get; set; }
        = string.Empty;

    public string? Comment { get; set; }

    public DateTime CreatedAtUtc { get; set; }
        = DateTime.UtcNow;
}

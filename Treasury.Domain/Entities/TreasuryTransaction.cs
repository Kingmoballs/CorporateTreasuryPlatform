namespace Treasury.Domain.Entities;

public class TreasuryTransaction
    : IOrganizationOwnedEntity
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public string Reference { get; set; }
        = string.Empty;

    public string TransactionType { get; set; }
        = string.Empty;

    public string Status { get; set; }
        = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; }
        = string.Empty;

    public string Description { get; set; }
        = string.Empty;

    public Guid? SourceAccountId { get; set; }

    public Guid? DestinationAccountId { get; set; }

    public Guid? TransferRequestId { get; set; }

    public Guid? InitiatedByUserId { get; set; }

    public Guid? CompletedByUserId { get; set; }

    public Guid? PaymentRequestId { get; set; }

    public string? Category { get; set; }

    public string? CounterpartyName { get; set; }

    public string? ExternalReference { get; set; }

    public Guid? ReversesTransactionId { get; set; }

    public TreasuryTransaction?
        ReversesTransaction { get; set; }

    public Guid? ReversalRequestId { get; set; }


    /*
    * Clients should reuse this key when retrying
    * the same request.
    */
    public string? IdempotencyKey { get; set; }

    public DateTime CreatedAtUtc { get; set; }
        = DateTime.UtcNow;

    public DateTime? CompletedAtUtc { get; set; }

    public ICollection<LedgerEntry>
        LedgerEntries { get; set; }
        = new List<LedgerEntry>();
}

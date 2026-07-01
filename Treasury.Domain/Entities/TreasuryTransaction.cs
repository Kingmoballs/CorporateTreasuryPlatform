namespace Treasury.Domain.Entities;

public class TreasuryTransaction
{
    public Guid Id { get; set; }

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

    public DateTime CreatedAtUtc { get; set; }
        = DateTime.UtcNow;

    public DateTime? CompletedAtUtc { get; set; }

    public ICollection<LedgerEntry>
        LedgerEntries { get; set; }
        = new List<LedgerEntry>();
}
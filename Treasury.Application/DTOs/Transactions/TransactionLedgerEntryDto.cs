namespace Treasury.Application.DTOs.Transactions;

public class TransactionLedgerEntryDto
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    public string AccountName { get; set; }
        = string.Empty;

    public string AccountNumber { get; set; }
        = string.Empty;

    public string EntryType { get; set; }
        = string.Empty;

    public decimal Amount { get; set; }

    public string Description { get; set; }
        = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}
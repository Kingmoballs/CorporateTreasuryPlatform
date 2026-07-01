namespace Treasury.Application.DTOs.Ledger;

public class LedgerEntryDto
{
    public decimal Amount { get; set; }

    public string EntryType { get; set; }
        = string.Empty;

    public string Description { get; set; }
        = string.Empty;
    
    public string? TransactionReference { get; set; }

    public DateTime CreatedAt { get; set; }
}
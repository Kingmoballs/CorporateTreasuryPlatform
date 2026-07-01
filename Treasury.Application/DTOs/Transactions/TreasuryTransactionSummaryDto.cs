namespace Treasury.Application.DTOs.Transactions;

public class TreasuryTransactionSummaryDto
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

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }
}
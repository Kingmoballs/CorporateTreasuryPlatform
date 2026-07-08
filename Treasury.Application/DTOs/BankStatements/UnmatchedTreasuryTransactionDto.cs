namespace Treasury.Application.DTOs.BankStatements;

public class UnmatchedTreasuryTransactionDto
{
    public Guid Id { get; set; }

    public string Reference { get; set; } = string.Empty;

    public string TransactionType { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public Guid? SourceAccountId { get; set; }

    public Guid? DestinationAccountId { get; set; }

    public string CashDirection { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    /*
     * Positive signed amount means inflow for the reconciled account.
     * Negative signed amount means outflow for the reconciled account.
     */
    public decimal SignedAmount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? Category { get; set; }

    public string? CounterpartyName { get; set; }

    public string? ExternalReference { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }
}
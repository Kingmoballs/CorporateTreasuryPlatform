namespace Treasury.Application.DTOs.BankStatements;

public class BankStatementLineResponseDto
{
    public Guid Id { get; set; }

    public Guid BankStatementImportId { get; set; }

    public Guid AccountId { get; set; }

    public int LineNumber { get; set; }

    public DateTime TransactionDateUtc { get; set; }

    public DateTime? ValueDateUtc { get; set; }

    public string Description { get; set; } = string.Empty;

    public string? BankReference { get; set; }

    public string? CounterpartyName { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public decimal? BalanceAfterTransaction { get; set; }

    public string ReconciliationStatus { get; set; } = string.Empty;

    public Guid? MatchedTreasuryTransactionId { get; set; }

    public string? MatchedTreasuryTransactionReference { get; set; }

    public DateTime? MatchedAtUtc { get; set; }

    public Guid? ReconciledByUserId { get; set; }

    public DateTime? ReconciledAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
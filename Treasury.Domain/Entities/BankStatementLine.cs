namespace Treasury.Domain.Entities;

public class BankStatementLine
    : IOrganizationOwnedEntity
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BankStatementImportId { get; set; }

    public BankStatementImport BankStatementImport { get; set; }
        = null!;

    public Guid AccountId { get; set; }

    public Account Account { get; set; } = null!;

    public int LineNumber { get; set; }

    public DateTime TransactionDateUtc { get; set; }

    public DateTime? ValueDateUtc { get; set; }

    public string Description { get; set; } = string.Empty;

    public string? BankReference { get; set; }

    public string? CounterpartyName { get; set; }

    /*
     * Positive amount means money came into the account.
     * Negative amount means money left the account.
     */
    public decimal Amount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public decimal? BalanceAfterTransaction { get; set; }

    public string ReconciliationStatus { get; set; }
        = "Unmatched";

    public Guid? MatchedTreasuryTransactionId { get; set; }

    public TreasuryTransaction? MatchedTreasuryTransaction { get; set; }

    public DateTime? MatchedAtUtc { get; set; }

    public Guid? ReconciledByUserId { get; set; }

    public User? ReconciledByUser { get; set; }

    public DateTime? ReconciledAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();
}

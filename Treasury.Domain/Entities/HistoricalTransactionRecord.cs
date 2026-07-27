namespace Treasury.Domain.Entities;

/*
 * Immutable, pre-platform transaction history. These rows
 * are reporting records, not operational treasury postings,
 * so they never alter an account balance or ledger.
 */
public class HistoricalTransactionRecord
    : IOrganizationOwnedEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrganizationId { get; set; }

    public Guid BatchId { get; set; }

    public HistoricalTransactionImportBatch Batch
        { get; set; } = null!;

    public Guid SourceRowId { get; set; }

    public HistoricalTransactionImportRow SourceRow
        { get; set; } = null!;

    public string ExternalReference { get; set; } =
        string.Empty;

    public Guid AccountId { get; set; }

    public Account Account { get; set; } = null!;

    public Guid? LegalEntityId { get; set; }

    public LegalEntity? LegalEntity { get; set; }

    public Guid? BusinessUnitId { get; set; }

    public BusinessUnit? BusinessUnit { get; set; }

    public DateTime TransactionDateUtc { get; set; }

    public DateTime? ValueDateUtc { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } =
        string.Empty;

    public string Direction { get; set; } =
        string.Empty;

    public string TransactionType { get; set; } =
        string.Empty;

    public string Description { get; set; } =
        string.Empty;

    public string? Category { get; set; }

    public string? CounterpartyName { get; set; }

    public DateTime CommittedAtUtc { get; set; } =
        DateTime.UtcNow;

    public Guid CommittedByUserId { get; set; }

    public User CommittedByUser { get; set; } =
        null!;
}

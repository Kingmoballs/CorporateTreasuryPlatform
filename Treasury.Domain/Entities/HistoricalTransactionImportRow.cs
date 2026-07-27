namespace Treasury.Domain.Entities;

/*
 * A validated-or-rejected staging row. This entity has no
 * relationship that can mutate an account balance, ledger
 * entry or treasury transaction.
 */
public class HistoricalTransactionImportRow
    : IOrganizationOwnedEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrganizationId { get; set; }

    public Guid BatchId { get; set; }

    public HistoricalTransactionImportBatch Batch
        { get; set; } = null!;

    public int RowNumber { get; set; }

    public string? ExternalReference { get; set; }

    public string AccountNumber { get; set; } =
        string.Empty;

    public Guid? AccountId { get; set; }

    public Account? Account { get; set; }

    public string? LegalEntityCode { get; set; }

    public Guid? LegalEntityId { get; set; }

    public LegalEntity? LegalEntity { get; set; }

    public string? BusinessUnitCode { get; set; }

    public Guid? BusinessUnitId { get; set; }

    public BusinessUnit? BusinessUnit { get; set; }

    public DateTime? TransactionDateUtc { get; set; }

    public DateTime? ValueDateUtc { get; set; }

    public decimal? Amount { get; set; }

    public string? Currency { get; set; }

    public string? Direction { get; set; }

    public string? TransactionType { get; set; }

    public string? Description { get; set; }

    public string? Category { get; set; }

    public string? CounterpartyName { get; set; }

    public string RawDataJson { get; set; } = "{}";

    public string ValidationErrorsJson { get; set; } =
        "[]";

    public string Fingerprint { get; set; } =
        string.Empty;

    public bool IsValid { get; set; }

    public Guid? PostedTreasuryTransactionId
        { get; set; }

    public TreasuryTransaction?
        PostedTreasuryTransaction { get; set; }

    public DateTime CreatedAtUtc { get; set; } =
        DateTime.UtcNow;
}

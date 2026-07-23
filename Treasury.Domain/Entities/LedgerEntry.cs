namespace Treasury.Domain.Entities;

public class LedgerEntry
    : IOrganizationOwnedEntity
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid AccountId { get; set; }

    public Account Account { get; set; } = null!;

    public decimal Amount { get; set; }

    public string EntryType { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public Guid? TreasuryTransactionId { get; set; }

    public TreasuryTransaction?
        TreasuryTransaction { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

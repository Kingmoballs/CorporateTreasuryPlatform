namespace Treasury.Domain.Entities;

public class CashFlowForecastItem
    : IOrganizationOwnedEntity
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid? AccountId { get; set; }

    public Account? Account { get; set; }

    public string Direction { get; set; } = "Outflow";

    public decimal Amount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public DateTime ExpectedDateUtc { get; set; }

    public string Category { get; set; } = string.Empty;

    public string? CounterpartyName { get; set; }

    public string Description { get; set; } = string.Empty;

    public string SourceType { get; set; } = "Manual";

    public string Status { get; set; } = "Active";

    public Guid? CreatedByUserId { get; set; }

    public User? CreatedByUser { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public Guid? CancelledByUserId { get; set; }

    public User? CancelledByUser { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public Guid? RealizedTreasuryTransactionId { get; set; }

    public TreasuryTransaction? RealizedTreasuryTransaction { get; set; }

    public DateTime? RealizedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();
}

namespace Treasury.Domain.Entities;

public class InvestmentLimit
{
    public Guid Id { get; set; }

    public Guid CounterpartyId { get; set; }

    public Counterparty Counterparty { get; set; } =
        null!;

    public string Currency { get; set; } = string.Empty;

    /*
     * Use "All" for a counterparty-wide limit or a
     * supported product such as "FixedDeposit".
     */
    public string InvestmentType { get; set; } = "All";

    public decimal MaximumExposureAmount { get; set; }

    /*
     * An alert will eventually be raised when utilization
     * reaches this percentage of the maximum exposure.
     */
    public decimal WarningThresholdPercentage
        { get; set; } = 80m;

    public DateTime EffectiveFromUtc { get; set; }

    public DateTime? EffectiveToUtc { get; set; }

    public bool IsActive { get; set; } = true;

    public string? Notes { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public User? CreatedByUser { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    public User? UpdatedByUser { get; set; }

    public DateTime CreatedAtUtc { get; set; } =
        DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } =
        DateTime.UtcNow;

    public Guid ConcurrencyToken { get; set; } =
        Guid.NewGuid();
}
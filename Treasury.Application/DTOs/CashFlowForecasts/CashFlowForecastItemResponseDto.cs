namespace Treasury.Application.DTOs.CashFlowForecasts;

public class CashFlowForecastItemResponseDto
{
    public Guid Id { get; set; }

    public Guid? AccountId { get; set; }

    public string? AccountName { get; set; }

    public string Direction { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public DateTime ExpectedDateUtc { get; set; }

    public string Category { get; set; } = string.Empty;

    public string? CounterpartyName { get; set; }

    public string Description { get; set; } = string.Empty;

    public string SourceType { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public Guid? CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public Guid? CancelledByUserId { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public Guid? RealizedTreasuryTransactionId { get; set; }

    public DateTime? RealizedAtUtc { get; set; }
}
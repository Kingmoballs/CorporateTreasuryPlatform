namespace Treasury.Application.DTOs.CashFlowForecasts;

public class CreateCashFlowForecastItemDto
{
    public Guid? AccountId { get; set; }

    public string Direction { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public DateTime ExpectedDateUtc { get; set; }

    public string Category { get; set; } = string.Empty;

    public string? CounterpartyName { get; set; }

    public string Description { get; set; } = string.Empty;

    public string SourceType { get; set; } = "Manual";
}
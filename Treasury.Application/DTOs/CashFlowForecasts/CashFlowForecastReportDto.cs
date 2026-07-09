namespace Treasury.Application.DTOs.CashFlowForecasts;

public class CashFlowForecastReportDto
{
    public DateTime GeneratedAtUtc { get; set; }

    public Guid? AccountId { get; set; }

    public string? AccountName { get; set; }

    public string Currency { get; set; } = string.Empty;

    public DateTime FromUtc { get; set; }

    public DateTime ToUtc { get; set; }

    public decimal OpeningAvailableBalance { get; set; }

    public decimal TotalExpectedInflow { get; set; }

    public decimal TotalExpectedOutflow { get; set; }

    public decimal NetMovement { get; set; }

    public decimal ProjectedClosingBalance { get; set; }

    public decimal MinimumProjectedBalance { get; set; }

    public decimal MinimumLiquidityThreshold { get; set; }

    public int LiquidityGapDayCount { get; set; }

    public List<CashFlowForecastDailyBucketDto> DailyForecasts { get; set; }
        = new();
}
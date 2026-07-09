namespace Treasury.Application.DTOs.CashFlowForecasts;

public class CashFlowForecastDailyBucketDto
{
    public DateTime DateUtc { get; set; }

    public decimal OpeningBalance { get; set; }

    public decimal ExpectedInflow { get; set; }

    public decimal ExpectedOutflow { get; set; }

    public decimal NetMovement { get; set; }

    public decimal ClosingBalance { get; set; }

    public bool IsLiquidityGap { get; set; }

    public decimal LiquidityGapAmount { get; set; }

    public List<CashFlowForecastItemResponseDto> Items { get; set; }
        = new();
}
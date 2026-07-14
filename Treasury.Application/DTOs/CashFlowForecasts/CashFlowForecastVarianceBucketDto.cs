namespace Treasury.Application.DTOs.CashFlowForecasts;

public class CashFlowForecastVarianceBucketDto
{
    public string Currency { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public int ForecastItemCount { get; set; }

    public int ActualTransactionCount { get; set; }

    public decimal ForecastedInflow { get; set; }

    public decimal ActualInflow { get; set; }

    public decimal InflowVariance { get; set; }

    public decimal ForecastedOutflow { get; set; }

    public decimal ActualOutflow { get; set; }

    public decimal OutflowVariance { get; set; }

    public decimal ForecastedNetMovement { get; set; }

    public decimal ActualNetMovement { get; set; }

    public decimal NetVariance { get; set; }

    public decimal? NetVariancePercentage { get; set; }
}
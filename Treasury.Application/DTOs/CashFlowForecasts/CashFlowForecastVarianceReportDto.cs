namespace Treasury.Application.DTOs.CashFlowForecasts;

public class CashFlowForecastVarianceReportDto
{
    public DateTime GeneratedAtUtc { get; set; }

    public Guid? AccountId { get; set; }

    public string? AccountName { get; set; }

    public string Currency { get; set; } = string.Empty;

    public DateTime FromUtc { get; set; }

    public DateTime ToUtc { get; set; }

    public int ForecastItemCount { get; set; }

    public int ActualTransactionCount { get; set; }

    public decimal TotalForecastedInflow { get; set; }

    public decimal TotalActualInflow { get; set; }

    public decimal TotalInflowVariance { get; set; }

    public decimal TotalForecastedOutflow { get; set; }

    public decimal TotalActualOutflow { get; set; }

    public decimal TotalOutflowVariance { get; set; }

    public decimal ForecastedNetMovement { get; set; }

    public decimal ActualNetMovement { get; set; }

    public decimal NetVariance { get; set; }

    public decimal? NetVariancePercentage { get; set; }

    public IReadOnlyList<CashFlowForecastVarianceBucketDto>
        Buckets { get; set; }
        = Array.Empty<CashFlowForecastVarianceBucketDto>();
}
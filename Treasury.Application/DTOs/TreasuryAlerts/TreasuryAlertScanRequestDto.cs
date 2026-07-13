namespace Treasury.Application.DTOs.TreasuryAlerts;

public class TreasuryAlertScanRequestDto
{
    public decimal LowLiquidityThreshold { get; set; } = 1_000_000m;

    public decimal ForecastLiquidityThreshold { get; set; } = 0m;

    public int ForecastDays { get; set; } = 30;

    public int PendingApprovalAgeHours { get; set; } = 24;

    public int ReconciliationLookbackDays { get; set; } = 30;

    public string? Currency { get; set; }

    public bool IncludeLowLiquidity { get; set; } = true;

    public bool IncludeForecastLiquidityGaps { get; set; } = true;

    public bool IncludePendingApprovals { get; set; } = true;

    public bool IncludeReconciliationExceptions { get; set; } = true;
}
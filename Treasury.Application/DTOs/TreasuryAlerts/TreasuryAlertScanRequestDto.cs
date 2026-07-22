namespace Treasury.Application.DTOs.TreasuryAlerts;

public class TreasuryAlertScanRequestDto
{
    public decimal LowLiquidityThreshold { get; set; } =
        1_000_000m;

    public decimal ForecastLiquidityThreshold { get; set; } =
        0m;

    public int ForecastDays { get; set; } = 30;

    public int PendingApprovalAgeHours { get; set; } = 24;

    public int ReconciliationLookbackDays { get; set; } = 30;

    public int InvestmentMaturityWarningDays { get; set; } = 14;

    public decimal InvestmentConcentrationThresholdPercentage
        { get; set; } = 50m;

    public string? Currency { get; set; }

    public bool IncludeLowLiquidity { get; set; } = true;

    public bool IncludeForecastLiquidityGaps { get; set; } =
        true;

    public bool IncludePendingApprovals { get; set; } = true;

    public bool IncludeReconciliationExceptions { get; set; } =
        true;

    public bool IncludeInvestmentMaturityAlerts { get; set; } =
        true;

    public bool IncludeInvestmentConcentrationAlerts
        { get; set; } = true;

    public bool IncludeInvestmentLimitAlerts
        { get; set; } = true;
}
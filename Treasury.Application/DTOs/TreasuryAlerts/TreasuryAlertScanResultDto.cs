namespace Treasury.Application.DTOs.TreasuryAlerts;

public class TreasuryAlertScanResultDto
{
    public DateTime GeneratedAtUtc { get; set; }

    public int CreatedAlertCount { get; set; }

    public int SkippedDuplicateCount { get; set; }

    public int LowLiquidityAlertCount { get; set; }

    public int ForecastLiquidityGapAlertCount { get; set; }

    public int PendingApprovalAlertCount { get; set; }

    public int ReconciliationExceptionAlertCount { get; set; }

    public int InvestmentMaturityAlertCount { get; set; }

    public int InvestmentOverdueAlertCount { get; set; }

    public int InvestmentConcentrationAlertCount { get; set; }

    public int InvestmentLimitWarningAlertCount
        { get; set; }

    public int InvestmentLimitBreachAlertCount
        { get; set; }

    public List<TreasuryAlertResponseDto> CreatedAlerts
        { get; set; } = new();
}
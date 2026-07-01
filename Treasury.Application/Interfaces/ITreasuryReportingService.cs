using Treasury.Application.DTOs.Reporting;

namespace Treasury.Application.Interfaces;

public interface ITreasuryReportingService
{
    Task<BalanceAggregationDto>
        GetBalanceAggregation();

    Task<CashPositionDashboardDto>
        GetCashPositionDashboard();
    
    Task<LiquidityReportDto>
        GetLiquidityReport(
            DateTime? fromUtc,
            DateTime? toUtc);
}
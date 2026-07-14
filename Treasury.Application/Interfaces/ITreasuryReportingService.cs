using Treasury.Application.DTOs.Reporting;
using Treasury.Application.DTOs.Exports;

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
    
    Task<CsvExportDto> ExportLiquidityReportCsv(
        DateTime? fromUtc,
        DateTime? toUtc);
}
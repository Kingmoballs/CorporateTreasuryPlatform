using Treasury.Application.DTOs.Reporting;
using Treasury.Application.DTOs.Exports;

namespace Treasury.Application.Interfaces;

public interface ITreasuryReportingService
{
    Task<BalanceAggregationDto>
        GetBalanceAggregation(
            Guid? legalEntityId = null,
            Guid? businessUnitId = null);

    Task<CashPositionDashboardDto>
        GetCashPositionDashboard(
            Guid? legalEntityId = null,
            Guid? businessUnitId = null);
    
    Task<LiquidityReportDto>
        GetLiquidityReport(
            DateTime? fromUtc,
            DateTime? toUtc,
            Guid? legalEntityId = null,
            Guid? businessUnitId = null);
    
    Task<CsvExportDto> ExportLiquidityReportCsv(
        DateTime? fromUtc,
        DateTime? toUtc,
        Guid? legalEntityId = null,
        Guid? businessUnitId = null);
}

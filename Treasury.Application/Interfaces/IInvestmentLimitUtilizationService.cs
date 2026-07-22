using Treasury.Application.DTOs.InvestmentLimits;
using Treasury.Application.DTOs.Exports;

namespace Treasury.Application.Interfaces;

public interface IInvestmentLimitUtilizationService
{
    Task<InvestmentLimitUtilizationReportDto>
        GetUtilization(
            InvestmentLimitUtilizationQueryDto query);
    
    Task<CsvExportDto> ExportCsv(
        InvestmentLimitUtilizationQueryDto query);
}
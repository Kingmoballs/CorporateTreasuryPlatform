using Treasury.Application.DTOs.InvestmentLimits;

namespace Treasury.Application.Interfaces;

public interface IInvestmentLimitUtilizationService
{
    Task<InvestmentLimitUtilizationReportDto>
        GetUtilization(
            InvestmentLimitUtilizationQueryDto query);
}
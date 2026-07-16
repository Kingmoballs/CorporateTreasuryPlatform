using Treasury.Application.DTOs.InvestmentPlacements;

namespace Treasury.Application.Interfaces;

public interface IInvestmentAccrualService
{
    Task<InvestmentAccrualReportDto> GetReport(
        InvestmentAccrualQueryDto query);
}
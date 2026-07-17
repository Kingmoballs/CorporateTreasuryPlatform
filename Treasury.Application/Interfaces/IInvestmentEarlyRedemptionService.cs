using Treasury.Application.DTOs.InvestmentPlacements;

namespace Treasury.Application.Interfaces;

public interface IInvestmentEarlyRedemptionService
{
    Task<InvestmentEarlyRedemptionQuoteDto> GetQuote(
        Guid investmentPlacementId,
        InvestmentEarlyRedemptionQuoteRequestDto request);
}
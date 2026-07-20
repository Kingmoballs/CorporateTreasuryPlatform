using Treasury.Application.DTOs.InvestmentPlacements;

namespace Treasury.Application.Interfaces;

public interface IInvestmentRolloverService
{
    Task<InvestmentRolloverQuoteDto> GetQuote(
        Guid investmentPlacementId,
        InvestmentRolloverQuoteRequestDto request);
}
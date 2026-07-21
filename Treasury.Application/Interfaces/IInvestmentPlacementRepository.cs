using Treasury.Application.DTOs.InvestmentPlacements;
using Treasury.Domain.Entities;

namespace Treasury.Application.Interfaces;

public interface IInvestmentPlacementRepository
{
    Task Add(InvestmentPlacement placement);

    Task<InvestmentPlacement?> GetById(Guid id);

    Task<bool> ReferenceExists(string reference);

    Task<(IReadOnlyList<InvestmentPlacement> Items, int TotalCount)>
        Search(InvestmentPlacementQueryDto query);
    
    Task<List<InvestmentPlacement>> GetDueForMaturity(
        DateTime asOfUtc,
        int maxRows);
    
    Task<IReadOnlyList<InvestmentPlacement>>
        GetForReporting(
            InvestmentPortfolioQueryDto query);
    
    Task<IReadOnlyList<InvestmentPlacement>>
        GetForLimitUtilization(
            Guid? counterpartyId,
            string? currency);

    void Update(InvestmentPlacement placement);

    Task SaveChanges();
}
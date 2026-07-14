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

    void Update(InvestmentPlacement placement);

    Task SaveChanges();
}
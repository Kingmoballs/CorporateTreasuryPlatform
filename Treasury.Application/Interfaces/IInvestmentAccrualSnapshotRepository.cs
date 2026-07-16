using Treasury.Application.DTOs.InvestmentPlacements;
using Treasury.Domain.Entities;

namespace Treasury.Application.Interfaces;

public interface IInvestmentAccrualSnapshotRepository
{
    Task<HashSet<Guid>> GetExistingPlacementIds(
        DateTime snapshotDateUtc,
        IReadOnlyCollection<Guid> placementIds);

    Task AddRange(
        IReadOnlyCollection<InvestmentAccrualSnapshot>
            snapshots);

    Task<(
        IReadOnlyList<InvestmentAccrualSnapshot> Items,
        int TotalCount)> Search(
            InvestmentAccrualSnapshotQueryDto query);
    
    Task<IReadOnlyList<InvestmentAccrualSnapshot>>
        GetForExport(
            InvestmentAccrualSnapshotQueryDto query,
            int maxRows);

    Task SaveChanges();
}
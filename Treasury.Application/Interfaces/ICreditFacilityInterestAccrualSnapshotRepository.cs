using Treasury.Application.DTOs.CreditFacilityAccruals;
using Treasury.Domain.Entities;

namespace Treasury.Application.Interfaces;

public interface
    ICreditFacilityInterestAccrualSnapshotRepository
{
    Task<DateTime?> GetLatestSnapshotDate(
        Guid creditFacilityId);

    Task<IReadOnlyList<CreditFacilityDrawdown>>
        GetDrawdowns(
            Guid creditFacilityId,
            DateTime toExclusiveUtc);

    Task<IReadOnlyList<CreditFacilityRepayment>>
        GetRepayments(
            Guid creditFacilityId,
            DateTime toExclusiveUtc);

    Task AddRange(
        IReadOnlyCollection<
            CreditFacilityInterestAccrualSnapshot>
            snapshots);

    Task<(
        IReadOnlyList<
            CreditFacilityInterestAccrualSnapshot> Items,
        int TotalCount)> Search(
            CreditFacilityAccrualSnapshotQueryDto query);

    Task SaveChanges();
}
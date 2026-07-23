using Microsoft.EntityFrameworkCore;
using Treasury.Application.DTOs.CreditFacilityAccruals;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;

namespace Treasury.Infrastructure.Repositories;

public class CreditFacilityInterestAccrualSnapshotRepository
    : ICreditFacilityInterestAccrualSnapshotRepository
{
    private readonly TreasuryDbContext _context;

    public CreditFacilityInterestAccrualSnapshotRepository(
        TreasuryDbContext context)
    {
        _context = context;
    }

    public async Task<DateTime?>
        GetLatestSnapshotDate(
            Guid creditFacilityId)
    {
        return await _context
            .CreditFacilityInterestAccrualSnapshots
            .AsNoTracking()
            .Where(snapshot =>
                snapshot.CreditFacilityId ==
                    creditFacilityId)
            .Select(snapshot =>
                (DateTime?)snapshot.SnapshotDateUtc)
            .MaxAsync();
    }

    public async Task<
        IReadOnlyList<CreditFacilityDrawdown>>
        GetDrawdowns(
            Guid creditFacilityId,
            DateTime toExclusiveUtc)
    {
        return await _context.CreditFacilityDrawdowns
            .AsNoTracking()
            .Where(drawdown =>
                drawdown.CreditFacilityId ==
                    creditFacilityId &&
                drawdown.DrawdownDateUtc <
                    toExclusiveUtc)
            .OrderBy(drawdown =>
                drawdown.DrawdownDateUtc)
            .ToListAsync();
    }

    public async Task<
        IReadOnlyList<CreditFacilityRepayment>>
        GetRepayments(
            Guid creditFacilityId,
            DateTime toExclusiveUtc)
    {
        return await _context.CreditFacilityRepayments
            .AsNoTracking()
            .Where(repayment =>
                repayment.CreditFacilityId ==
                    creditFacilityId &&
                repayment.RepaymentDateUtc <
                    toExclusiveUtc)
            .OrderBy(repayment =>
                repayment.RepaymentDateUtc)
            .ToListAsync();
    }

    public async Task AddRange(
        IReadOnlyCollection<
            CreditFacilityInterestAccrualSnapshot>
            snapshots)
    {
        if (snapshots.Count == 0)
        {
            return;
        }

        await _context
            .CreditFacilityInterestAccrualSnapshots
            .AddRangeAsync(snapshots);
    }

    public async Task<(
        IReadOnlyList<
            CreditFacilityInterestAccrualSnapshot> Items,
        int TotalCount)> Search(
            CreditFacilityAccrualSnapshotQueryDto query)
    {
        var snapshots =
            _context
                .CreditFacilityInterestAccrualSnapshots
                .AsNoTracking()
                .AsQueryable();

        if (query.CreditFacilityId.HasValue)
        {
            snapshots =
                snapshots.Where(snapshot =>
                    snapshot.CreditFacilityId ==
                        query.CreditFacilityId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Currency))
        {
            snapshots =
                snapshots.Where(snapshot =>
                    snapshot.Currency ==
                        query.Currency);
        }

        if (query.SnapshotDateFromUtc.HasValue)
        {
            snapshots =
                snapshots.Where(snapshot =>
                    snapshot.SnapshotDateUtc >=
                        query.SnapshotDateFromUtc.Value);
        }

        if (query.SnapshotDateToUtc.HasValue)
        {
            snapshots =
                snapshots.Where(snapshot =>
                    snapshot.SnapshotDateUtc <=
                        query.SnapshotDateToUtc.Value);
        }

        var totalCount =
            await snapshots.CountAsync();

        var items =
            await snapshots
                .OrderByDescending(snapshot =>
                    snapshot.SnapshotDateUtc)
                .ThenBy(snapshot =>
                    snapshot.FacilityReference)
                .Skip(
                    (query.Page - 1) *
                    query.PageSize)
                .Take(query.PageSize)
                .ToListAsync();

        return (items, totalCount);
    }

    public async Task SaveChanges()
    {
        await _context.SaveChangesAsync();
    }
}
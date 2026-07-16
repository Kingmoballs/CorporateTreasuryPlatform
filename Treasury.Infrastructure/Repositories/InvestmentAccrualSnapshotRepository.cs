using Microsoft.EntityFrameworkCore;
using Treasury.Application.DTOs.InvestmentPlacements;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;

namespace Treasury.Infrastructure.Repositories;

public class InvestmentAccrualSnapshotRepository
    : IInvestmentAccrualSnapshotRepository
{
    private readonly TreasuryDbContext _context;

    public InvestmentAccrualSnapshotRepository(
        TreasuryDbContext context)
    {
        _context =
            context;
    }

    public async Task<HashSet<Guid>>
        GetExistingPlacementIds(
            DateTime snapshotDateUtc,
            IReadOnlyCollection<Guid> placementIds)
    {
        if (placementIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var ids =
            placementIds.ToList();

        var existingIds =
            await _context.InvestmentAccrualSnapshots
                .AsNoTracking()
                .Where(snapshot =>
                    snapshot.SnapshotDateUtc ==
                        snapshotDateUtc &&
                    ids.Contains(
                        snapshot.InvestmentPlacementId))
                .Select(snapshot =>
                    snapshot.InvestmentPlacementId)
                .ToListAsync();

        return existingIds.ToHashSet();
    }

    public async Task AddRange(
        IReadOnlyCollection<InvestmentAccrualSnapshot>
            snapshots)
    {
        if (snapshots.Count == 0)
        {
            return;
        }

        await _context.InvestmentAccrualSnapshots
            .AddRangeAsync(snapshots);
    }

    public async Task<(
        IReadOnlyList<InvestmentAccrualSnapshot> Items,
        int TotalCount)> Search(
            InvestmentAccrualSnapshotQueryDto query)
    {
        var snapshots =
            _context.InvestmentAccrualSnapshots
                .AsNoTracking()
                .AsQueryable();

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

        if (!string.IsNullOrWhiteSpace(query.Currency))
        {
            snapshots =
                snapshots.Where(snapshot =>
                    snapshot.Currency ==
                    query.Currency);
        }

        if (!string.IsNullOrWhiteSpace(
            query.InstitutionName))
        {
            snapshots =
                snapshots.Where(snapshot =>
                    EF.Functions.ILike(
                        snapshot.InstitutionName,
                        $"%{query.InstitutionName}%"));
        }

        if (query.InvestmentPlacementId.HasValue)
        {
            snapshots =
                snapshots.Where(snapshot =>
                    snapshot.InvestmentPlacementId ==
                    query.InvestmentPlacementId.Value);
        }

        var totalCount =
            await snapshots.CountAsync();

        var items =
            await snapshots
                .OrderByDescending(snapshot =>
                    snapshot.SnapshotDateUtc)
                .ThenBy(snapshot =>
                    snapshot.Currency)
                .ThenBy(snapshot =>
                    snapshot.InvestmentReference)
                .Skip(
                    (query.Page - 1) *
                    query.PageSize)
                .Take(query.PageSize)
                .ToListAsync();

        return (items, totalCount);
    }

    public async Task<
    IReadOnlyList<InvestmentAccrualSnapshot>>
        GetForExport(
            InvestmentAccrualSnapshotQueryDto query,
            int maxRows)
    {
        var snapshots =
            _context.InvestmentAccrualSnapshots
                .AsNoTracking()
                .AsQueryable();

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

        if (!string.IsNullOrWhiteSpace(query.Currency))
        {
            snapshots =
                snapshots.Where(snapshot =>
                    snapshot.Currency ==
                    query.Currency);
        }

        if (!string.IsNullOrWhiteSpace(
            query.InstitutionName))
        {
            snapshots =
                snapshots.Where(snapshot =>
                    EF.Functions.ILike(
                        snapshot.InstitutionName,
                        $"%{query.InstitutionName}%"));
        }

        if (query.InvestmentPlacementId.HasValue)
        {
            snapshots =
                snapshots.Where(snapshot =>
                    snapshot.InvestmentPlacementId ==
                    query.InvestmentPlacementId.Value);
        }

        return await snapshots
            .OrderByDescending(snapshot =>
                snapshot.SnapshotDateUtc)
            .ThenBy(snapshot =>
                snapshot.Currency)
            .ThenBy(snapshot =>
                snapshot.InvestmentReference)
            .Take(maxRows)
            .ToListAsync();
    }

    public async Task SaveChanges()
    {
        await _context.SaveChangesAsync();
    }
}
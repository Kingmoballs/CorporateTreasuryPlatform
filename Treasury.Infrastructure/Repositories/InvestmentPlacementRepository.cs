using Microsoft.EntityFrameworkCore;
using Treasury.Application.DTOs.InvestmentPlacements;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Repositories;

public class InvestmentPlacementRepository
    : IInvestmentPlacementRepository
{
    private readonly TreasuryDbContext _context;

    public InvestmentPlacementRepository(
        TreasuryDbContext context)
    {
        _context = context;
    }

    public async Task Add(
        InvestmentPlacement placement)
    {
        await _context.InvestmentPlacements
            .AddAsync(placement);
    }

    public async Task<InvestmentPlacement?> GetById(
        Guid id)
    {
        return await _context.InvestmentPlacements
            .Include(placement =>
                placement.SourceAccount)
            .Include(placement =>
                placement.FundingTreasuryTransaction)
            .Include(placement =>
                placement.MaturityForecastItem)
            .Include(placement =>
                placement.RedemptionAccount)
            .Include(placement =>
                placement.RedemptionTreasuryTransaction)
            .FirstOrDefaultAsync(placement =>
                placement.Id == id);
    }

    public async Task<bool> ReferenceExists(
        string reference)
    {
        return await _context.InvestmentPlacements
            .AsNoTracking()
            .AnyAsync(placement =>
                placement.Reference == reference);
    }

    public async Task<(
        IReadOnlyList<InvestmentPlacement> Items,
        int TotalCount)> Search(
        InvestmentPlacementQueryDto query)
    {
        var page =
            query.Page < 1 ? 1 : query.Page;

        var pageSize =
            query.PageSize < 1
                ? 50
                : Math.Min(query.PageSize, 100);

        var placements =
            _context.InvestmentPlacements
                .AsNoTracking()
                .Include(placement =>
                    placement.SourceAccount)
                .Include(placement =>
                    placement.FundingTreasuryTransaction)
                .Include(placement =>
                    placement.MaturityForecastItem)
                .Include(placement =>
                    placement.RedemptionAccount)
                .Include(placement =>
                    placement.RedemptionTreasuryTransaction)
                .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            placements =
                placements.Where(placement =>
                    placement.Status == query.Status);
        }

        if (!string.IsNullOrWhiteSpace(
                query.InvestmentType))
        {
            placements =
                placements.Where(placement =>
                    placement.InvestmentType ==
                    query.InvestmentType);
        }

        if (!string.IsNullOrWhiteSpace(
                query.InstitutionName))
        {
            var institutionName =
                query.InstitutionName.Trim();

            placements =
                placements.Where(placement =>
                    EF.Functions.ILike(
                        placement.InstitutionName,
                        $"%{institutionName}%"));
        }

        if (query.SourceAccountId.HasValue)
        {
            placements =
                placements.Where(placement =>
                    placement.SourceAccountId ==
                    query.SourceAccountId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Currency))
        {
            placements =
                placements.Where(placement =>
                    placement.Currency ==
                    query.Currency);
        }

        if (query.MaturityFromUtc.HasValue)
        {
            placements =
                placements.Where(placement =>
                    placement.MaturityDateUtc >=
                    query.MaturityFromUtc.Value);
        }

        if (query.MaturityToUtc.HasValue)
        {
            placements =
                placements.Where(placement =>
                    placement.MaturityDateUtc <=
                    query.MaturityToUtc.Value);
        }

        var totalCount =
            await placements.CountAsync();

        var items =
            await placements
                .OrderBy(placement =>
                    placement.MaturityDateUtc)
                .ThenByDescending(placement =>
                    placement.CreatedAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

        return (items, totalCount);
    }

    public async Task<List<InvestmentPlacement>>
        GetDueForMaturity(
            DateTime asOfUtc,
            int maxRows)
    {
        return await _context.InvestmentPlacements
            .Where(placement =>
                placement.Status ==
                    InvestmentPlacementStatuses.Active &&
                placement.MaturityDateUtc <= asOfUtc)
            .OrderBy(placement =>
                placement.MaturityDateUtc)
            .Take(maxRows)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<InvestmentPlacement>>
        GetForReporting(
            InvestmentPortfolioQueryDto query)
    {
        var placements =
            _context.InvestmentPlacements
                .AsNoTracking()
                .Where(placement =>
                    placement.Status ==
                        InvestmentPlacementStatuses.Active ||
                    placement.Status ==
                        InvestmentPlacementStatuses.Matured ||
                    (query.IncludeRedeemed &&
                    placement.Status ==
                        InvestmentPlacementStatuses.Redeemed))
                .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Currency))
        {
            placements =
                placements.Where(placement =>
                    placement.Currency ==
                        query.Currency);
        }

        if (!string.IsNullOrWhiteSpace(
                query.InstitutionName))
        {
            placements =
                placements.Where(placement =>
                    EF.Functions.ILike(
                        placement.InstitutionName,
                        $"%{query.InstitutionName}%"));
        }

        if (query.MaturityFromUtc.HasValue)
        {
            placements =
                placements.Where(placement =>
                    placement.MaturityDateUtc >=
                        query.MaturityFromUtc.Value);
        }

        if (query.MaturityToUtc.HasValue)
        {
            placements =
                placements.Where(placement =>
                    placement.MaturityDateUtc <=
                        query.MaturityToUtc.Value);
        }

        return await placements
            .OrderBy(placement =>
                placement.MaturityDateUtc)
            .ThenBy(placement =>
                placement.InstitutionName)
            .ToListAsync();
    }

    public void Update(
        InvestmentPlacement placement)
    {
        _context.InvestmentPlacements
            .Update(placement);
    }

    public async Task SaveChanges()
    {
        await _context.SaveChangesAsync();
    }
}
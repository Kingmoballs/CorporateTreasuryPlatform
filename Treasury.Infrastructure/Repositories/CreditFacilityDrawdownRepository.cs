using Microsoft.EntityFrameworkCore;
using Treasury.Application.DTOs.CreditFacilityDrawdowns;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;

namespace Treasury.Infrastructure.Repositories;

public class CreditFacilityDrawdownRepository
    : ICreditFacilityDrawdownRepository
{
    private readonly TreasuryDbContext _context;

    public CreditFacilityDrawdownRepository(
        TreasuryDbContext context)
    {
        _context = context;
    }

    public async Task Add(
        CreditFacilityDrawdown drawdown)
    {
        await _context.CreditFacilityDrawdowns
            .AddAsync(drawdown);
    }

    public async Task<CreditFacilityDrawdown?>
        GetById(Guid id)
    {
        return await _context.CreditFacilityDrawdowns
            .AsNoTracking()
            .Include(drawdown =>
                drawdown.CreditFacility)
            .ThenInclude(facility =>
                facility.LenderCounterparty)
            .Include(drawdown =>
                drawdown.SettlementAccount)
            .Include(drawdown =>
                drawdown.TreasuryTransaction)
            .FirstOrDefaultAsync(drawdown =>
                drawdown.Id == id);
    }

    public async Task<CreditFacilityDrawdown?>
        GetByIdempotencyKey(
            string idempotencyKey)
    {
        return await _context.CreditFacilityDrawdowns
            .AsNoTracking()
            .Include(drawdown =>
                drawdown.CreditFacility)
            .ThenInclude(facility =>
                facility.LenderCounterparty)
            .Include(drawdown =>
                drawdown.SettlementAccount)
            .Include(drawdown =>
                drawdown.TreasuryTransaction)
            .FirstOrDefaultAsync(drawdown =>
                drawdown.IdempotencyKey ==
                    idempotencyKey);
    }

    public async Task<bool> ReferenceExists(
        string reference)
    {
        return await _context.CreditFacilityDrawdowns
            .AsNoTracking()
            .AnyAsync(drawdown =>
                drawdown.Reference == reference);
    }

    public async Task<(
        IReadOnlyList<CreditFacilityDrawdown> Items,
        int TotalCount)> Search(
            Guid creditFacilityId,
            CreditFacilityDrawdownQueryDto query)
    {
        var page =
            query.Page < 1 ? 1 : query.Page;

        var pageSize =
            query.PageSize < 1
                ? 50
                : Math.Min(query.PageSize, 100);

        var drawdowns =
            _context.CreditFacilityDrawdowns
                .AsNoTracking()
                .Include(drawdown =>
                    drawdown.CreditFacility)
                .ThenInclude(facility =>
                    facility.LenderCounterparty)
                .Include(drawdown =>
                    drawdown.SettlementAccount)
                .Include(drawdown =>
                    drawdown.TreasuryTransaction)
                .Where(drawdown =>
                    drawdown.CreditFacilityId ==
                        creditFacilityId)
                .AsQueryable();

        if (query.FromUtc.HasValue)
        {
            drawdowns =
                drawdowns.Where(drawdown =>
                    drawdown.DrawdownDateUtc >=
                        query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            /*
             * ToUtc is exclusive. For example,
             * 2026-08-01 includes records before August 1.
             */
            drawdowns =
                drawdowns.Where(drawdown =>
                    drawdown.DrawdownDateUtc <
                        query.ToUtc.Value);
        }

        var totalCount =
            await drawdowns.CountAsync();

        var items =
            await drawdowns
                .OrderByDescending(drawdown =>
                    drawdown.DrawdownDateUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

        return (items, totalCount);
    }
}
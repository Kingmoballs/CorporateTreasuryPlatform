using Microsoft.EntityFrameworkCore;
using Treasury.Application.DTOs.CreditFacilityRepayments;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;

namespace Treasury.Infrastructure.Repositories;

public class CreditFacilityRepaymentRepository
    : ICreditFacilityRepaymentRepository
{
    private readonly TreasuryDbContext _context;

    public CreditFacilityRepaymentRepository(
        TreasuryDbContext context)
    {
        _context = context;
    }

    public async Task Add(
        CreditFacilityRepayment repayment)
    {
        await _context.CreditFacilityRepayments
            .AddAsync(repayment);
    }

    public async Task<CreditFacilityRepayment?>
        GetById(Guid id)
    {
        return await _context.CreditFacilityRepayments
            .AsNoTracking()
            .Include(repayment =>
                repayment.CreditFacility)
            .ThenInclude(facility =>
                facility.LenderCounterparty)
            .Include(repayment =>
                repayment.SettlementAccount)
            .Include(repayment =>
                repayment.TreasuryTransaction)
            .FirstOrDefaultAsync(repayment =>
                repayment.Id == id);
    }

    public async Task<CreditFacilityRepayment?>
        GetByIdempotencyKey(
            string idempotencyKey)
    {
        return await _context.CreditFacilityRepayments
            .AsNoTracking()
            .Include(repayment =>
                repayment.CreditFacility)
            .ThenInclude(facility =>
                facility.LenderCounterparty)
            .Include(repayment =>
                repayment.SettlementAccount)
            .Include(repayment =>
                repayment.TreasuryTransaction)
            .FirstOrDefaultAsync(repayment =>
                repayment.IdempotencyKey ==
                    idempotencyKey);
    }

    public async Task<bool> ReferenceExists(
        string reference)
    {
        return await _context.CreditFacilityRepayments
            .AsNoTracking()
            .AnyAsync(repayment =>
                repayment.Reference == reference);
    }

    public async Task<(
        IReadOnlyList<CreditFacilityRepayment> Items,
        int TotalCount)> Search(
            Guid creditFacilityId,
            CreditFacilityRepaymentQueryDto query)
    {
        var page =
            query.Page < 1 ? 1 : query.Page;

        var pageSize =
            query.PageSize < 1
                ? 50
                : Math.Min(query.PageSize, 100);

        var repayments =
            _context.CreditFacilityRepayments
                .AsNoTracking()
                .Include(repayment =>
                    repayment.CreditFacility)
                .ThenInclude(facility =>
                    facility.LenderCounterparty)
                .Include(repayment =>
                    repayment.SettlementAccount)
                .Include(repayment =>
                    repayment.TreasuryTransaction)
                .Where(repayment =>
                    repayment.CreditFacilityId ==
                        creditFacilityId)
                .AsQueryable();

        if (query.FromUtc.HasValue)
        {
            repayments =
                repayments.Where(repayment =>
                    repayment.RepaymentDateUtc >=
                        query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            /*
             * ToUtc is exclusive.
             */
            repayments =
                repayments.Where(repayment =>
                    repayment.RepaymentDateUtc <
                        query.ToUtc.Value);
        }

        var totalCount =
            await repayments.CountAsync();

        var items =
            await repayments
                .OrderByDescending(repayment =>
                    repayment.RepaymentDateUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

        return (items, totalCount);
    }
}
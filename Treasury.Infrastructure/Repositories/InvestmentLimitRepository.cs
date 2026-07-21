using Microsoft.EntityFrameworkCore;
using Treasury.Application.DTOs.InvestmentLimits;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;

namespace Treasury.Infrastructure.Repositories;

public class InvestmentLimitRepository
    : IInvestmentLimitRepository
{
    private readonly TreasuryDbContext _context;

    public InvestmentLimitRepository(
        TreasuryDbContext context)
    {
        _context = context;
    }

    public async Task Add(
        InvestmentLimit investmentLimit)
    {
        await _context.InvestmentLimits
            .AddAsync(investmentLimit);
    }

    public async Task<InvestmentLimit?> GetById(
        Guid id)
    {
        return await _context.InvestmentLimits
            .Include(limit =>
                limit.Counterparty)
            .FirstOrDefaultAsync(limit =>
                limit.Id == id);
    }

    public async Task<(
        IReadOnlyList<InvestmentLimit> Items,
        int TotalCount)> Search(
            InvestmentLimitQueryDto query)
    {
        var limits =
            _context.InvestmentLimits
                .AsNoTracking()
                .Include(limit =>
                    limit.Counterparty)
                .AsQueryable();

        if (query.CounterpartyId.HasValue)
        {
            limits =
                limits.Where(limit =>
                    limit.CounterpartyId ==
                        query.CounterpartyId.Value);
        }

        if (!string.IsNullOrWhiteSpace(
                query.Currency))
        {
            limits =
                limits.Where(limit =>
                    limit.Currency ==
                        query.Currency);
        }

        if (!string.IsNullOrWhiteSpace(
                query.InvestmentType))
        {
            limits =
                limits.Where(limit =>
                    limit.InvestmentType ==
                        query.InvestmentType);
        }

        if (query.IsActive.HasValue)
        {
            limits =
                limits.Where(limit =>
                    limit.IsActive ==
                        query.IsActive.Value);
        }

        if (query.AsOfUtc.HasValue)
        {
            var asOfUtc =
                query.AsOfUtc.Value;

            limits =
                limits.Where(limit =>
                    limit.EffectiveFromUtc <= asOfUtc &&
                    (limit.EffectiveToUtc == null ||
                     limit.EffectiveToUtc > asOfUtc));
        }

        var totalCount =
            await limits.CountAsync();

        var items =
            await limits
                .OrderBy(limit =>
                    limit.Counterparty.Name)
                .ThenBy(limit =>
                    limit.Currency)
                .ThenBy(limit =>
                    limit.InvestmentType)
                .ThenByDescending(limit =>
                    limit.EffectiveFromUtc)
                .Skip(
                    (query.Page - 1) *
                    query.PageSize)
                .Take(query.PageSize)
                .ToListAsync();

        return (items, totalCount);
    }

    public async Task<bool>
        HasOverlappingActiveLimit(
            Guid counterpartyId,
            string currency,
            string investmentType,
            DateTime effectiveFromUtc,
            DateTime? effectiveToUtc,
            Guid? excludedLimitId)
    {
        var limits =
            _context.InvestmentLimits
                .AsNoTracking()
                .Where(limit =>
                    limit.IsActive &&
                    limit.CounterpartyId ==
                        counterpartyId &&
                    limit.Currency ==
                        currency &&
                    limit.InvestmentType ==
                        investmentType);

        if (excludedLimitId.HasValue)
        {
            limits =
                limits.Where(limit =>
                    limit.Id !=
                        excludedLimitId.Value);
        }

        /*
         * EffectiveToUtc is exclusive. This allows one
         * limit to end at exactly the time the next begins.
         */
        limits =
            limits.Where(limit =>
                limit.EffectiveToUtc == null ||
                limit.EffectiveToUtc >
                    effectiveFromUtc);

        if (effectiveToUtc.HasValue)
        {
            var requestedEnd =
                effectiveToUtc.Value;

            limits =
                limits.Where(limit =>
                    limit.EffectiveFromUtc <
                        requestedEnd);
        }

        return await limits.AnyAsync();
    }

    public async Task<IReadOnlyList<InvestmentLimit>>
        GetApplicableActiveLimits(
            Guid? counterpartyId,
            string? currency,
            DateTime asOfUtc)
    {
        var limits =
            _context.InvestmentLimits
                .AsNoTracking()
                .Include(limit =>
                    limit.Counterparty)
                .Where(limit =>
                    limit.IsActive &&
                    limit.Counterparty.IsActive &&
                    limit.EffectiveFromUtc <= asOfUtc &&
                    (limit.EffectiveToUtc == null ||
                    limit.EffectiveToUtc > asOfUtc))
                .AsQueryable();

        if (counterpartyId.HasValue)
        {
            limits =
                limits.Where(limit =>
                    limit.CounterpartyId ==
                        counterpartyId.Value);
        }

        if (!string.IsNullOrWhiteSpace(currency))
        {
            limits =
                limits.Where(limit =>
                    limit.Currency == currency);
        }

        return await limits
            .OrderBy(limit =>
                limit.Counterparty.Name)
            .ThenBy(limit =>
                limit.Currency)
            .ThenBy(limit =>
                limit.InvestmentType)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<InvestmentLimit>>
        GetApplicableActiveLimitsForUpdate(
            Guid counterpartyId,
            string currency,
            string investmentType,
            DateTime asOfUtc)
    {
        /*
        * Lock both the overall "All" limit and the specific
        * product limit in a consistent ID order.
        *
        * A second activation for the same counterparty waits
        * here. After the first transaction commits, the second
        * transaction recalculates exposure using fresh data.
        */
        return await _context.InvestmentLimits
            .FromSqlInterpolated(
                $@"SELECT *
                FROM ""InvestmentLimits""
                WHERE ""CounterpartyId"" = {counterpartyId}
                    AND ""Currency"" = {currency}
                    AND ""IsActive"" = TRUE
                    AND ""EffectiveFromUtc"" <= {asOfUtc}
                    AND (
                        ""EffectiveToUtc"" IS NULL
                        OR ""EffectiveToUtc"" > {asOfUtc}
                    )
                    AND ""InvestmentType""
                        IN ('All', {investmentType})
                ORDER BY ""Id""
                FOR UPDATE")
            .ToListAsync();
    }

    public void Update(
        InvestmentLimit investmentLimit)
    {
        _context.InvestmentLimits
            .Update(investmentLimit);
    }

    public async Task SaveChanges()
    {
        await _context.SaveChangesAsync();
    }
}
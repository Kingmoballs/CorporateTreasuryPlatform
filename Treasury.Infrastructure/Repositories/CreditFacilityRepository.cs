using Microsoft.EntityFrameworkCore;
using Treasury.Application.DTOs.CreditFacilities;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Repositories;

public class CreditFacilityRepository
    : ICreditFacilityRepository
{
    private readonly TreasuryDbContext _context;

    public CreditFacilityRepository(
        TreasuryDbContext context)
    {
        _context = context;
    }

    public async Task Add(
        CreditFacility facility)
    {
        await _context.CreditFacilities
            .AddAsync(facility);
    }

    public async Task<CreditFacility?> GetById(
        Guid id)
    {
        return await _context.CreditFacilities
            .Include(facility =>
                facility.LenderCounterparty)
            .Include(facility =>
                facility.SettlementAccount)
            .FirstOrDefaultAsync(facility =>
                facility.Id == id);
    }

    public async Task<CreditFacility?>
        GetByActivationIdempotencyKey(
            string idempotencyKey)
    {
        return await _context.CreditFacilities
            .AsNoTracking()
            .FirstOrDefaultAsync(facility =>
                facility.ActivationIdempotencyKey ==
                    idempotencyKey);
    }

    public async Task<bool> ReferenceExists(
        string reference)
    {
        return await _context.CreditFacilities
            .AsNoTracking()
            .AnyAsync(facility =>
                facility.Reference == reference);
    }

    public async Task<(
        IReadOnlyList<CreditFacility> Items,
        int TotalCount)> Search(
            CreditFacilityQueryDto query)
    {
        var page =
            query.Page < 1 ? 1 : query.Page;

        var pageSize =
            query.PageSize < 1
                ? 50
                : Math.Min(query.PageSize, 100);

        var facilities =
            _context.CreditFacilities
                .AsNoTracking()
                .Include(facility =>
                    facility.LenderCounterparty)
                .Include(facility =>
                    facility.SettlementAccount)
                .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            facilities =
                facilities.Where(facility =>
                    facility.Status == query.Status);
        }

        if (!string.IsNullOrWhiteSpace(
                query.FacilityType))
        {
            facilities =
                facilities.Where(facility =>
                    facility.FacilityType ==
                        query.FacilityType);
        }

        if (!string.IsNullOrWhiteSpace(
                query.FacilityName))
        {
            var name = query.FacilityName.Trim();

            facilities =
                facilities.Where(facility =>
                    EF.Functions.ILike(
                        facility.FacilityName,
                        $"%{name}%"));
        }

        if (query.LenderCounterpartyId.HasValue)
        {
            facilities =
                facilities.Where(facility =>
                    facility.LenderCounterpartyId ==
                        query.LenderCounterpartyId.Value);
        }

        if (query.SettlementAccountId.HasValue)
        {
            facilities =
                facilities.Where(facility =>
                    facility.SettlementAccountId ==
                        query.SettlementAccountId.Value);
        }

        if (!string.IsNullOrWhiteSpace(
                query.Currency))
        {
            facilities =
                facilities.Where(facility =>
                    facility.Currency ==
                        query.Currency);
        }

        if (query.MaturityFromUtc.HasValue)
        {
            facilities =
                facilities.Where(facility =>
                    facility.MaturityDateUtc >=
                        query.MaturityFromUtc.Value);
        }

        if (query.MaturityToUtc.HasValue)
        {
            facilities =
                facilities.Where(facility =>
                    facility.MaturityDateUtc <=
                        query.MaturityToUtc.Value);
        }

        var totalCount =
            await facilities.CountAsync();

        var items =
            await facilities
                .OrderBy(facility =>
                    facility.MaturityDateUtc)
                .ThenByDescending(facility =>
                    facility.CreatedAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

        return (items, totalCount);
    }

    public void Update(
        CreditFacility facility)
    {
        _context.CreditFacilities.Update(facility);
    }

    public async Task SaveChanges()
    {
        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<CreditFacility>>
        GetForInterestAccrual(
            Guid? creditFacilityId,
            DateTime asOfDateUtc,
            int maxRows)
    {
        var toExclusiveUtc =
            asOfDateUtc.Date.AddDays(1);

        var facilities =
            _context.CreditFacilities
                .Include(facility =>
                    facility.LenderCounterparty)
                .Include(facility =>
                    facility.SettlementAccount)
                .Where(facility =>
                    facility.Status ==
                        CreditFacilityStatuses.Active ||
                    facility.Status ==
                        CreditFacilityStatuses.Suspended ||
                    facility.Status ==
                        CreditFacilityStatuses.Matured)
                .Where(facility =>
                    facility.StartDateUtc.Date <=
                        asOfDateUtc.Date)
                .Where(facility =>
                    facility.OutstandingPrincipalAmount > 0)
                .Where(facility =>
                    _context.CreditFacilityDrawdowns
                        .Any(drawdown =>
                            drawdown.CreditFacilityId ==
                                facility.Id &&
                            drawdown.DrawdownDateUtc <
                                toExclusiveUtc))
                .AsQueryable();

        if (creditFacilityId.HasValue)
        {
            facilities =
                facilities.Where(facility =>
                    facility.Id ==
                        creditFacilityId.Value);
        }

        return await facilities
            .OrderBy(facility =>
                facility.MaturityDateUtc)
            .Take(maxRows)
            .ToListAsync();
    }
}
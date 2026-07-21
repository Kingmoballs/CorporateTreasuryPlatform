using Microsoft.EntityFrameworkCore;
using Treasury.Application.DTOs.Counterparties;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;

namespace Treasury.Infrastructure.Repositories;

public class CounterpartyRepository
    : ICounterpartyRepository
{
    private readonly TreasuryDbContext _context;

    public CounterpartyRepository(
        TreasuryDbContext context)
    {
        _context = context;
    }

    public async Task Add(
        Counterparty counterparty)
    {
        await _context.Counterparties
            .AddAsync(counterparty);
    }

    public async Task<Counterparty?> GetById(
        Guid id)
    {
        return await _context.Counterparties
            .FirstOrDefaultAsync(counterparty =>
                counterparty.Id == id);
    }

    public async Task<Counterparty?>
        GetByIdForUpdate(Guid id)
    {
        /*
        * This must be called inside an open transaction.
        * FOR UPDATE prevents counterparty deactivation from
        * racing with an investment activation.
        */
        return await _context.Counterparties
            .FromSqlInterpolated(
                $@"SELECT *
                FROM ""Counterparties""
                WHERE ""Id"" = {id}
                FOR UPDATE")
            .SingleOrDefaultAsync();
    }

    public async Task<bool> CodeExists(
        string code)
    {
        return await _context.Counterparties
            .AsNoTracking()
            .AnyAsync(counterparty =>
                counterparty.Code == code);
    }

    public async Task<(
        IReadOnlyList<Counterparty> Items,
        int TotalCount)> Search(
            CounterpartyQueryDto query)
    {
        var counterparties =
            _context.Counterparties
                .AsNoTracking()
                .AsQueryable();

        if (!string.IsNullOrWhiteSpace(
                query.Search))
        {
            var search = query.Search.Trim();

            counterparties =
                counterparties.Where(counterparty =>
                    EF.Functions.ILike(
                        counterparty.Code,
                        $"%{search}%") ||
                    EF.Functions.ILike(
                        counterparty.Name,
                        $"%{search}%") ||
                    (counterparty.SwiftCode != null &&
                     EF.Functions.ILike(
                         counterparty.SwiftCode,
                         $"%{search}%")));
        }

        if (!string.IsNullOrWhiteSpace(
                query.CounterpartyType))
        {
            counterparties =
                counterparties.Where(counterparty =>
                    counterparty.CounterpartyType ==
                        query.CounterpartyType);
        }

        if (query.IsActive.HasValue)
        {
            counterparties =
                counterparties.Where(counterparty =>
                    counterparty.IsActive ==
                        query.IsActive.Value);
        }

        var totalCount =
            await counterparties.CountAsync();

        var items =
            await counterparties
                .OrderBy(counterparty =>
                    counterparty.Name)
                .Skip(
                    (query.Page - 1) *
                    query.PageSize)
                .Take(query.PageSize)
                .ToListAsync();

        return (items, totalCount);
    }

    public void Update(
        Counterparty counterparty)
    {
        _context.Counterparties
            .Update(counterparty);
    }

    public async Task SaveChanges()
    {
        await _context.SaveChangesAsync();
    }
}
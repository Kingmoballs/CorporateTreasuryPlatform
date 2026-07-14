using Microsoft.EntityFrameworkCore;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Repositories;

public class CashFlowForecastRepository
    : ICashFlowForecastRepository
{
    private readonly TreasuryDbContext _context;

    public CashFlowForecastRepository(
        TreasuryDbContext context)
    {
        _context = context;
    }

    public async Task Add(
        CashFlowForecastItem forecastItem)
    {
        await _context.CashFlowForecastItems
            .AddAsync(forecastItem);
    }

    public async Task<CashFlowForecastItem?> GetById(
        Guid id)
    {
        return await _context.CashFlowForecastItems
            .Include(item =>
                item.Account)
            .Include(item =>
                item.RealizedTreasuryTransaction)
            .FirstOrDefaultAsync(item =>
                item.Id == id);
    }

    public async Task<List<CashFlowForecastItem>> GetActiveForPeriod(
        Guid? accountId,
        string? currency,
        DateTime fromUtc,
        DateTime toUtc)
    {
        var query =
            _context.CashFlowForecastItems
                .AsNoTracking()
                .Include(item =>
                    item.Account)
                .Where(item =>
                    item.Status ==
                    CashFlowForecastStatus.Active)
                .Where(item =>
                    item.ExpectedDateUtc >= fromUtc &&
                    item.ExpectedDateUtc <= toUtc);

        if (accountId.HasValue)
        {
            query =
                query.Where(item =>
                    item.AccountId == accountId.Value);
        }

        if (!string.IsNullOrWhiteSpace(currency))
        {
            var normalizedCurrency =
                currency.Trim().ToUpperInvariant();

            query =
                query.Where(item =>
                    item.Currency == normalizedCurrency);
        }

        return await query
            .OrderBy(item =>
                item.ExpectedDateUtc)
            .ThenBy(item =>
                item.Direction)
            .ToListAsync();
    }

    public async Task<List<CashFlowForecastItem>> GetForVarianceReport(
        Guid? accountId,
        string currency,
        DateTime fromUtc,
        DateTime toUtc)
    {
        var normalizedCurrency =
            currency.Trim().ToUpperInvariant();

        var query =
            _context.CashFlowForecastItems
                .AsNoTracking()
                .Include(item =>
                    item.Account)
                .Where(item =>
                    item.Status !=
                    CashFlowForecastStatus.Cancelled)
                .Where(item =>
                    item.Currency == normalizedCurrency)
                .Where(item =>
                    item.ExpectedDateUtc >= fromUtc &&
                    item.ExpectedDateUtc <= toUtc);

        if (accountId.HasValue)
        {
            query =
                query.Where(item =>
                    item.AccountId == accountId.Value);
        }

        return await query
            .OrderBy(item =>
                item.ExpectedDateUtc)
            .ThenBy(item =>
                item.Category)
            .ToListAsync();
    }

    public void Update(
        CashFlowForecastItem forecastItem)
    {
        _context.CashFlowForecastItems
            .Update(forecastItem);
    }

    public async Task<bool> TreasuryTransactionAlreadyRealized(
        Guid treasuryTransactionId,
        Guid? excludeForecastItemId = null)
    {
        var query =
            _context.CashFlowForecastItems
                .AsQueryable()
                .Where(item =>
                    item.RealizedTreasuryTransactionId ==
                    treasuryTransactionId);

        if (excludeForecastItemId.HasValue)
        {
            query =
                query.Where(item =>
                    item.Id != excludeForecastItemId.Value);
        }

        return await query.AnyAsync();
    }

    public async Task SaveChanges()
    {
        await _context.SaveChangesAsync();
    }
}
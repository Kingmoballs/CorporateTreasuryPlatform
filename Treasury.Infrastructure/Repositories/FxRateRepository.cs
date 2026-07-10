using Microsoft.EntityFrameworkCore;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;

namespace Treasury.Infrastructure.Repositories;

public class FxRateRepository
    : IFxRateRepository
{
    private readonly TreasuryDbContext _context;

    public FxRateRepository(
        TreasuryDbContext context)
    {
        _context = context;
    }

    public async Task Add(
        FxRate fxRate)
    {
        await _context.FxRates
            .AddAsync(fxRate);
    }

    public async Task<FxRate?> GetById(
        Guid id)
    {
        return await _context.FxRates
            .Include(rate =>
                rate.CreatedByUser)
            .FirstOrDefaultAsync(rate =>
                rate.Id == id);
    }

    public async Task<FxRate?> GetLatestRate(
        string fromCurrency,
        string toCurrency,
        DateTime? asOfUtc)
    {
        var normalizedFrom =
            fromCurrency.Trim().ToUpperInvariant();

        var normalizedTo =
            toCurrency.Trim().ToUpperInvariant();

        var query =
            _context.FxRates
                .AsNoTracking()
                .Where(rate =>
                    rate.IsActive &&
                    rate.FromCurrency == normalizedFrom &&
                    rate.ToCurrency == normalizedTo);

        if (asOfUtc.HasValue)
        {
            query =
                query.Where(rate =>
                    rate.RateDateUtc <= asOfUtc.Value);
        }

        return await query
            .OrderByDescending(rate =>
                rate.RateDateUtc)
            .FirstOrDefaultAsync();
    }

    public async Task<List<FxRate>> GetRates(
        string? fromCurrency,
        string? toCurrency,
        DateTime? fromUtc,
        DateTime? toUtc)
    {
        var query =
            _context.FxRates
                .AsNoTracking()
                .Include(rate =>
                    rate.CreatedByUser)
                .AsQueryable();

        if (!string.IsNullOrWhiteSpace(fromCurrency))
        {
            var normalizedFrom =
                fromCurrency.Trim().ToUpperInvariant();

            query =
                query.Where(rate =>
                    rate.FromCurrency == normalizedFrom);
        }

        if (!string.IsNullOrWhiteSpace(toCurrency))
        {
            var normalizedTo =
                toCurrency.Trim().ToUpperInvariant();

            query =
                query.Where(rate =>
                    rate.ToCurrency == normalizedTo);
        }

        if (fromUtc.HasValue)
        {
            query =
                query.Where(rate =>
                    rate.RateDateUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query =
                query.Where(rate =>
                    rate.RateDateUtc <= toUtc.Value);
        }

        return await query
            .OrderByDescending(rate =>
                rate.RateDateUtc)
            .ThenBy(rate =>
                rate.FromCurrency)
            .ThenBy(rate =>
                rate.ToCurrency)
            .ToListAsync();
    }

    public async Task<bool> RateExistsForDate(
        string fromCurrency,
        string toCurrency,
        DateTime rateDateUtc)
    {
        var normalizedFrom =
            fromCurrency.Trim().ToUpperInvariant();

        var normalizedTo =
            toCurrency.Trim().ToUpperInvariant();

        return await _context.FxRates
            .AnyAsync(rate =>
                rate.FromCurrency == normalizedFrom &&
                rate.ToCurrency == normalizedTo &&
                rate.RateDateUtc == rateDateUtc);
    }

    public void Update(
        FxRate fxRate)
    {
        _context.FxRates.Update(fxRate);
    }

    public async Task SaveChanges()
    {
        await _context.SaveChangesAsync();
    }
}
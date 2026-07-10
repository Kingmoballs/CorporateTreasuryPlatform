using Treasury.Domain.Entities;

namespace Treasury.Application.Interfaces;

public interface IFxRateRepository
{
    Task Add(FxRate fxRate);

    Task<FxRate?> GetById(Guid id);

    Task<FxRate?> GetLatestRate(
        string fromCurrency,
        string toCurrency,
        DateTime? asOfUtc);

    Task<List<FxRate>> GetRates(
        string? fromCurrency,
        string? toCurrency,
        DateTime? fromUtc,
        DateTime? toUtc);

    Task<bool> RateExistsForDate(
        string fromCurrency,
        string toCurrency,
        DateTime rateDateUtc);

    void Update(FxRate fxRate);

    Task SaveChanges();
}
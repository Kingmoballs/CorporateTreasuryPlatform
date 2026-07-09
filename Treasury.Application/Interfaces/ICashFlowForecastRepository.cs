using Treasury.Domain.Entities;

namespace Treasury.Application.Interfaces;

public interface ICashFlowForecastRepository
{
    Task Add(CashFlowForecastItem forecastItem);

    Task<CashFlowForecastItem?> GetById(Guid id);

    Task<List<CashFlowForecastItem>> GetActiveForPeriod(
        Guid? accountId,
        string? currency,
        DateTime fromUtc,
        DateTime toUtc);

    void Update(CashFlowForecastItem forecastItem);

    Task<bool> TreasuryTransactionAlreadyRealized(
        Guid treasuryTransactionId,
        Guid? excludeForecastItemId = null);

    Task SaveChanges();
}
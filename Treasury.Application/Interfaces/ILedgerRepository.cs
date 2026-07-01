using Treasury.Domain.Entities;

namespace Treasury.Application.Interfaces;

public interface ILedgerRepository
{
    Task Add(LedgerEntry entry);

    Task<List<LedgerEntry>>
    GetByAccountId(Guid accountId);

    Task<List<LedgerEntry>> GetByDateRange(
        DateTime fromUtc,
        DateTime toUtc);

    Task SaveChanges();
}
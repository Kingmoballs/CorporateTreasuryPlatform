using Treasury.Domain.Entities;

namespace Treasury.Application.Interfaces;

public interface ILedgerRepository
{
    Task Add(LedgerEntry entry);

    Task SaveChanges();
}
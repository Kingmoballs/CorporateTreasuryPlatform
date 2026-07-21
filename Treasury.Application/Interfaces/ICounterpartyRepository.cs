using Treasury.Application.DTOs.Counterparties;
using Treasury.Domain.Entities;

namespace Treasury.Application.Interfaces;

public interface ICounterpartyRepository
{
    Task Add(Counterparty counterparty);

    Task<Counterparty?> GetById(Guid id);
    Task<Counterparty?> GetByIdForUpdate(Guid id);
    Task<bool> CodeExists(string code);

    Task<(
        IReadOnlyList<Counterparty> Items,
        int TotalCount)> Search(
            CounterpartyQueryDto query);

    void Update(Counterparty counterparty);

    Task SaveChanges();
}
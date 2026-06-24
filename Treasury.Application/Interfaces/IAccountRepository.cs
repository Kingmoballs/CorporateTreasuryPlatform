using Treasury.Domain.Entities;

namespace Treasury.Application.Interfaces;

public interface IAccountRepository
{
    Task Add(Account account);

    Task<Account?> GetById(Guid id);

    Task<List<Account>> GetAll();

    Task<bool> AccountNumberExists(
        string accountNumber);
    
    void Update(Account account);

    Task SaveChanges();

    Task BeginTransaction();

    Task CommitTransaction();

    Task RollbackTransaction();
}
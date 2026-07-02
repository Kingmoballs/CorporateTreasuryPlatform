using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;

namespace Treasury.Tests.Integration;

/*
 * This test-only decorator pauses both requests after
 * they read the same account balance and concurrency
 * token. It creates a reliable concurrency collision.
 */
public class CoordinatedAccountRepository
    : IAccountRepository
{
    private readonly IAccountRepository
        _innerRepository;

    private readonly AsyncTestBarrier
        _readBarrier;

    public CoordinatedAccountRepository(
        IAccountRepository innerRepository,
        AsyncTestBarrier readBarrier)
    {
        _innerRepository =
            innerRepository;

        _readBarrier =
            readBarrier;
    }

    public Task Add(Account account)
    {
        return _innerRepository.Add(account);
    }

    public async Task<Account?>
        GetById(Guid id)
    {
        var account =
            await _innerRepository
                .GetById(id);

        await _readBarrier
            .SignalAndWaitAsync();

        return account;
    }

    public Task<List<Account>> GetAll()
    {
        return _innerRepository.GetAll();
    }

    public Task<bool> AccountNumberExists(
        string accountNumber)
    {
        return _innerRepository
            .AccountNumberExists(
                accountNumber);
    }

    public void Update(Account account)
    {
        _innerRepository.Update(account);
    }

    public Task SaveChanges()
    {
        return _innerRepository.SaveChanges();
    }

    public Task BeginTransaction()
    {
        return _innerRepository
            .BeginTransaction();
    }

    public Task CommitTransaction()
    {
        return _innerRepository
            .CommitTransaction();
    }

    public Task RollbackTransaction()
    {
        return _innerRepository
            .RollbackTransaction();
    }
}
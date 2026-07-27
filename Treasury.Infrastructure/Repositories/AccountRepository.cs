using Microsoft.EntityFrameworkCore;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Storage;

public class AccountRepository
    : IAccountRepository
{
    private readonly TreasuryDbContext _context;

    private IDbContextTransaction?
    _transaction;

    public AccountRepository(
        TreasuryDbContext context)
    {
        _context = context;
    }

    public async Task Add(Account account)
    {
        await _context.Accounts.AddAsync(
            account);
    }

    public async Task<Account?>
        GetById(Guid id)
    {
        return await _context.Accounts
            .Include(x => x.AccountType)
            .Include(x => x.LegalEntity)
            .Include(x => x.BusinessUnit)
            .FirstOrDefaultAsync(
                x => x.Id == id);
    }

    public async Task<List<Account>>
        GetAll()
    {
        return await _context.Accounts
            .Include(x => x.AccountType)
            .Include(x => x.LegalEntity)
            .Include(x => x.BusinessUnit)
            .ToListAsync();
    }

    public async Task<bool>
        AccountNumberExists(
            string accountNumber)
    {
        return await _context.Accounts
            .AnyAsync(
                x => x.AccountNumber
                    == accountNumber);
    }

    public void Update(Account account)
    {
        _context.Accounts.Update(account);
    }

    public async Task SaveChanges()
    {
        await _context.SaveChangesAsync();
    }


    public async Task BeginTransaction()
    {
        _transaction =
            await _context.Database
                .BeginTransactionAsync();
    }

    public async Task CommitTransaction()
    {
        if (_transaction is null)
        {
            return;
        }

        try
        {
            await _transaction.CommitAsync();
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransaction()
    {
        if (_transaction is null)
        {
            return;
        }

        try
        {
            await _transaction.RollbackAsync();
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }
}

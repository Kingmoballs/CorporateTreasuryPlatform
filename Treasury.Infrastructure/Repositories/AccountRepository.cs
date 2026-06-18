using Microsoft.EntityFrameworkCore;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;

public class AccountRepository
    : IAccountRepository
{
    private readonly TreasuryDbContext _context;

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
            .FirstOrDefaultAsync(
                x => x.Id == id);
    }

    public async Task<List<Account>>
        GetAll()
    {
        return await _context.Accounts
            .Include(x => x.AccountType)
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

    public async Task SaveChanges()
    {
        await _context.SaveChangesAsync();
    }
}
using Microsoft.EntityFrameworkCore;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;

public class AccountTypeRepository
    : IAccountTypeRepository
{
    private readonly TreasuryDbContext _context;

    public AccountTypeRepository(
        TreasuryDbContext context)
    {
        _context = context;
    }

    public async Task<AccountType?>
        GetById(Guid id)
    {
        return await _context.AccountTypes
            .FirstOrDefaultAsync(
                x => x.Id == id);
    }
}
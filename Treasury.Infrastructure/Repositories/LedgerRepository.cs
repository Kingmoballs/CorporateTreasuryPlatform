using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Treasury.Infrastructure.Repositories;

public class LedgerRepository
    : ILedgerRepository
{
    private readonly TreasuryDbContext _context;

    public LedgerRepository(
        TreasuryDbContext context)
    {
        _context = context;
    }
    
    public async Task Add(LedgerEntry entry)
    {
        await _context.LedgerEntries
            .AddAsync(entry);
    }

    public async Task<List<LedgerEntry>>
    GetByAccountId(Guid accountId)
    {
        return await _context.LedgerEntries
            .Where(x => x.AccountId == accountId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task SaveChanges()
    {
        await _context.SaveChangesAsync();
    }
}
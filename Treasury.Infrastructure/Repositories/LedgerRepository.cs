using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;

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

    public async Task SaveChanges()
    {
        await _context.SaveChangesAsync();
    }
}
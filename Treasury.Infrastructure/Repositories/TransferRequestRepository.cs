using Microsoft.EntityFrameworkCore;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;

namespace Treasury.Infrastructure.Repositories;

public class TransferRequestRepository
    : ITransferRequestRepository
{
    private readonly TreasuryDbContext _context;

    public TransferRequestRepository(
        TreasuryDbContext context)
    {
        _context = context;
    }

    public async Task Add(
        TransferRequest request)
    {
        await _context.TransferRequests
            .AddAsync(request);
    }

    public async Task<TransferRequest?>
        GetById(Guid id)
    {
        return await _context.TransferRequests
            .FirstOrDefaultAsync(
                x => x.Id == id);
    }

    public async Task<List<TransferRequest>>
        GetPending()
    {
        return await _context.TransferRequests
            .Where(x => x.Status == "Pending")
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    public void Update(TransferRequest request)
    {
        _context.TransferRequests.Update(request);
    }

    public async Task SaveChanges()
    {
        await _context.SaveChangesAsync();
    }
}
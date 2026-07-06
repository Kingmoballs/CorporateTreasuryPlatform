using Microsoft.EntityFrameworkCore;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;
using Treasury.Shared.Constants;

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
        var nowUtc =
            DateTime.UtcNow;

        return await _context.TransferRequests
            .Where(request =>
                request.Status ==
                    ApprovalStatus.Pending &&
                (!request.ExpiresAtUtc.HasValue ||
                request.ExpiresAtUtc.Value > nowUtc))
            .OrderByDescending(request =>
                request.CreatedAt)
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
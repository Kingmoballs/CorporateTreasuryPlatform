using Microsoft.EntityFrameworkCore;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;

namespace Treasury.Infrastructure.Repositories;

public class ReversalRequestRepository
    : IReversalRequestRepository
{
    private readonly TreasuryDbContext _context;

    public ReversalRequestRepository(
        TreasuryDbContext context)
    {
        _context = context;
    }

    public async Task Add(
        ReversalRequest request)
    {
        await _context.ReversalRequests
            .AddAsync(request);
    }

    public async Task<ReversalRequest?>
        GetById(Guid id)
    {
        return await _context.ReversalRequests
            .Include(request =>
                request.OriginalTransaction)
            .FirstOrDefaultAsync(request =>
                request.Id == id);
    }

    public async Task<ReversalRequest?>
        GetByOriginalTransactionId(
            Guid transactionId)
    {
        return await _context.ReversalRequests
            .AsNoTracking()
            .Include(request =>
                request.OriginalTransaction)
            .FirstOrDefaultAsync(request =>
                request.OriginalTransactionId ==
                    transactionId);
    }

    public async Task<List<ReversalRequest>>
        GetPending()
    {
        return await _context.ReversalRequests
            .AsNoTracking()
            .Include(request =>
                request.OriginalTransaction)
            .Where(request =>
                request.Status == "Pending")
            .OrderByDescending(request =>
                request.CreatedAtUtc)
            .ToListAsync();
    }

    public void Update(
        ReversalRequest request)
    {
        _context.ReversalRequests
            .Update(request);
    }

    public async Task SaveChanges()
    {
        await _context.SaveChangesAsync();
    }
}
using Microsoft.EntityFrameworkCore;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;

namespace Treasury.Infrastructure.Repositories;

public class PaymentRequestRepository
    : IPaymentRequestRepository
{
    private readonly TreasuryDbContext _context;

    public PaymentRequestRepository(
        TreasuryDbContext context)
    {
        _context = context;
    }

    public async Task Add(
        PaymentRequest request)
    {
        await _context.PaymentRequests
            .AddAsync(request);
    }

    public async Task<PaymentRequest?>
        GetById(Guid id)
    {
        return await _context.PaymentRequests
            .FirstOrDefaultAsync(request =>
                request.Id == id);
    }

    public async Task<PaymentRequest?>
        GetByIdempotencyKey(string key)
    {
        return await _context.PaymentRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(request =>
                request.IdempotencyKey == key);
    }

    public async Task<List<PaymentRequest>>
        GetPending()
    {
        return await _context.PaymentRequests
            .AsNoTracking()
            .Where(request =>
                request.Status == "Pending")
            .OrderByDescending(request =>
                request.CreatedAtUtc)
            .ToListAsync();
    }

    public void Update(
        PaymentRequest request)
    {
        _context.PaymentRequests
            .Update(request);
    }

    public async Task SaveChanges()
    {
        await _context.SaveChangesAsync();
    }
}
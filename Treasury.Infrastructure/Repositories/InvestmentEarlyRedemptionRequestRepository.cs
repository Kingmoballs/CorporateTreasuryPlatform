using Microsoft.EntityFrameworkCore;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Repositories;

public class InvestmentEarlyRedemptionRequestRepository
    : IInvestmentEarlyRedemptionRequestRepository
{
    private readonly TreasuryDbContext _context;

    public InvestmentEarlyRedemptionRequestRepository(
        TreasuryDbContext context)
    {
        _context =
            context;
    }

    public async Task Add(
        InvestmentEarlyRedemptionRequest request)
    {
        await _context.InvestmentEarlyRedemptionRequests
            .AddAsync(request);
    }

    public Task<InvestmentEarlyRedemptionRequest?>
        GetById(Guid id)
    {
        return BaseQuery()
            .FirstOrDefaultAsync(request =>
                request.Id == id);
    }

    public Task<InvestmentEarlyRedemptionRequest?>
        GetByIdempotencyKey(
            string idempotencyKey)
    {
        return BaseQuery()
            .FirstOrDefaultAsync(request =>
                request.RequestIdempotencyKey ==
                    idempotencyKey);
    }

    public Task<List<InvestmentEarlyRedemptionRequest>>
        GetPending()
    {
        var nowUtc =
            DateTime.UtcNow;

        return BaseQuery()
            .Where(request =>
                (request.Status ==
                    InvestmentEarlyRedemptionStatuses.Pending ||
                 request.Status ==
                    InvestmentEarlyRedemptionStatuses.Approved) &&
                request.ExpiresAtUtc > nowUtc)
            .OrderBy(request =>
                request.ExpiresAtUtc)
            .ToListAsync();
    }

    public Task<bool> HasDecision(
        Guid requestId,
        Guid approverUserId)
    {
        return _context
            .InvestmentEarlyRedemptionDecisions
            .AnyAsync(decision =>
                decision
                    .InvestmentEarlyRedemptionRequestId ==
                        requestId &&
                decision.ApproverUserId ==
                    approverUserId);
    }

    public async Task AddDecision(
        InvestmentEarlyRedemptionDecision decision)
    {
        await _context
            .InvestmentEarlyRedemptionDecisions
            .AddAsync(decision);
    }

    public void Update(
        InvestmentEarlyRedemptionRequest request)
    {
        _context.InvestmentEarlyRedemptionRequests
            .Update(request);
    }

    public async Task SaveChanges()
    {
        await _context.SaveChangesAsync();
    }

    private IQueryable<
        InvestmentEarlyRedemptionRequest> BaseQuery()
    {
        return _context
            .InvestmentEarlyRedemptionRequests
            .Include(request =>
                request.InvestmentPlacement)
                .ThenInclude(placement =>
                    placement.MaturityForecastItem)
            .Include(request =>
                request.DestinationAccount)
            .Include(request =>
                request.Decisions)
            .AsSplitQuery();
    }
}

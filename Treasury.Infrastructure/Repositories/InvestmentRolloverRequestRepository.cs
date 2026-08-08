using Microsoft.EntityFrameworkCore;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Repositories;

public class InvestmentRolloverRequestRepository
    : IInvestmentRolloverRequestRepository
{
    private readonly TreasuryDbContext _context;

    public InvestmentRolloverRequestRepository(
        TreasuryDbContext context)
    {
        _context = context;
    }

    public async Task Add(
        InvestmentRolloverRequest request)
    {
        await _context.InvestmentRolloverRequests
            .AddAsync(request);
    }

    public Task<InvestmentRolloverRequest?> GetById(
        Guid id)
    {
        return BaseQuery()
            .FirstOrDefaultAsync(request =>
                request.Id == id);
    }

    public Task<InvestmentRolloverRequest?>
        GetByIdempotencyKey(
            string idempotencyKey)
    {
        return BaseQuery()
            .FirstOrDefaultAsync(request =>
                request.RequestIdempotencyKey ==
                idempotencyKey);
    }

    public Task<InvestmentRolloverRequest?>
        GetOpenForPlacement(
            Guid investmentPlacementId)
    {
        return BaseQuery()
            .FirstOrDefaultAsync(request =>
                request.OriginalInvestmentPlacementId ==
                    investmentPlacementId &&
                (request.Status ==
                    InvestmentRolloverStatuses.Pending ||
                 request.Status ==
                    InvestmentRolloverStatuses.Approved));
    }

    public Task<List<InvestmentRolloverRequest>>
        GetPending()
    {
        var nowUtc = DateTime.UtcNow;

        return BaseQuery()
            .Where(request =>
                (request.Status ==
                    InvestmentRolloverStatuses.Pending ||
                 request.Status ==
                    InvestmentRolloverStatuses.Approved) &&
                request.ExpiresAtUtc > nowUtc)
            .OrderBy(request =>
                request.ExpiresAtUtc)
            .ToListAsync();
    }

    public Task<bool> HasDecision(
        Guid requestId,
        Guid approverUserId)
    {
        return _context.InvestmentRolloverDecisions
            .AnyAsync(decision =>
                decision.InvestmentRolloverRequestId ==
                    requestId &&
                decision.ApproverUserId ==
                    approverUserId);
    }

    public async Task AddDecision(
        InvestmentRolloverDecision decision)
    {
        await _context.InvestmentRolloverDecisions
            .AddAsync(decision);
    }

    public async Task SaveChanges()
    {
        await _context.SaveChangesAsync();
    }

    private IQueryable<InvestmentRolloverRequest>
        BaseQuery()
    {
        return _context.InvestmentRolloverRequests
            .Include(request =>
                request.OriginalInvestmentPlacement)
                .ThenInclude(placement =>
                    placement.MaturityForecastItem)
            .Include(request =>
                request.OriginalInvestmentPlacement)
                .ThenInclude(placement =>
                    placement.SourceAccount)
            .Include(request =>
                request.OriginalInvestmentPlacement)
                .ThenInclude(placement =>
                    placement.Counterparty)
            .Include(request =>
                request.CashPayoutAccount)
            .Include(request =>
                request.NewInvestmentPlacement)
            .Include(request =>
                request.CashPayoutTreasuryTransaction)
            .Include(request =>
                request.Decisions)
            .AsSplitQuery();
    }
}

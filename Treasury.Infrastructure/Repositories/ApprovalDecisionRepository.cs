using Microsoft.EntityFrameworkCore;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;

namespace Treasury.Infrastructure.Repositories;

public class ApprovalDecisionRepository
    : IApprovalDecisionRepository
{
    private readonly TreasuryDbContext _context;

    public ApprovalDecisionRepository(
        TreasuryDbContext context)
    {
        _context = context;
    }

    public async Task Add(
        ApprovalDecision decision)
    {
        await _context.ApprovalDecisions
            .AddAsync(decision);
    }

    public async Task<bool>
        HasTransferDecision(
            Guid transferRequestId,
            Guid approverUserId)
    {
        return await _context.ApprovalDecisions
            .AnyAsync(decision =>
                decision.TransferRequestId ==
                    transferRequestId &&
                decision.ApproverUserId ==
                    approverUserId);
    }

    public async Task<bool>
        HasPaymentDecision(
            Guid paymentRequestId,
            Guid approverUserId)
    {
        return await _context.ApprovalDecisions
            .AnyAsync(decision =>
                decision.PaymentRequestId ==
                    paymentRequestId &&
                decision.ApproverUserId ==
                    approverUserId);
    }

    public async Task<bool>
        HasReversalDecision(
            Guid reversalRequestId,
            Guid approverUserId)
    {
        return await _context.ApprovalDecisions
            .AnyAsync(decision =>
                decision.ReversalRequestId ==
                    reversalRequestId &&
                decision.ApproverUserId ==
                    approverUserId);
    }

    public Task<List<ApprovalDecision>>
        GetForTransfer(Guid transferRequestId)
    {
        return _context.ApprovalDecisions
            .AsNoTracking()
            .Include(decision =>
                decision.Approver)
            .Where(decision =>
                decision.TransferRequestId ==
                    transferRequestId)
            .OrderBy(decision =>
                decision.CreatedAtUtc)
            .ToListAsync();
    }

    public Task<List<ApprovalDecision>>
        GetForPayment(Guid paymentRequestId)
    {
        return _context.ApprovalDecisions
            .AsNoTracking()
            .Include(decision =>
                decision.Approver)
            .Where(decision =>
                decision.PaymentRequestId ==
                    paymentRequestId)
            .OrderBy(decision =>
                decision.CreatedAtUtc)
            .ToListAsync();
    }

    public Task<List<ApprovalDecision>>
        GetForReversal(Guid reversalRequestId)
    {
        return _context.ApprovalDecisions
            .AsNoTracking()
            .Include(decision =>
                decision.Approver)
            .Where(decision =>
                decision.ReversalRequestId ==
                    reversalRequestId)
            .OrderBy(decision =>
                decision.CreatedAtUtc)
            .ToListAsync();
    }

    public async Task<bool>
        HasInvestmentPlacementDecision(
            Guid investmentPlacementId,
            Guid approverUserId)
    {
        return await _context.ApprovalDecisions
            .AnyAsync(decision =>
                decision.InvestmentPlacementId ==
                    investmentPlacementId &&
                decision.ApproverUserId ==
                    approverUserId);
    }

    public Task<List<ApprovalDecision>>
        GetForInvestmentPlacement(
            Guid investmentPlacementId)
    {
        return _context.ApprovalDecisions
            .AsNoTracking()
            .Include(decision =>
                decision.Approver)
            .Where(decision =>
                decision.InvestmentPlacementId ==
                    investmentPlacementId)
            .OrderBy(decision =>
                decision.CreatedAtUtc)
            .ToListAsync();
    }
}
using Microsoft.EntityFrameworkCore;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;

namespace Treasury.Infrastructure.Repositories;

public class ApprovalPolicyRepository
    : IApprovalPolicyRepository
{
    private readonly TreasuryDbContext _context;

    public ApprovalPolicyRepository(
        TreasuryDbContext context)
    {
        _context = context;
    }

    public async Task<ApprovalPolicy?>
        GetActive(
            string operationType,
            string currency)
    {
        return await _context.ApprovalPolicies
            .AsNoTracking()
            .FirstOrDefaultAsync(policy =>
                policy.OperationType ==
                    operationType &&
                policy.Currency ==
                    currency &&
                policy.IsActive);
    }

    public async Task<ApprovalPolicy?>
        GetByKey(
            string operationType,
            string currency)
    {
        return await _context.ApprovalPolicies
            .FirstOrDefaultAsync(policy =>
                policy.OperationType ==
                    operationType &&
                policy.Currency ==
                    currency);
    }

    public async Task<List<ApprovalPolicy>>
        GetAll()
    {
        return await _context.ApprovalPolicies
            .AsNoTracking()
            .OrderBy(policy =>
                policy.OperationType)
            .ThenBy(policy =>
                policy.Currency)
            .ToListAsync();
    }

    public async Task Add(
        ApprovalPolicy policy)
    {
        await _context.ApprovalPolicies
            .AddAsync(policy);
    }

    public async Task SaveChanges()
    {
        await _context.SaveChangesAsync();
    }
}
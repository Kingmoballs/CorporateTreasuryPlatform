using Microsoft.EntityFrameworkCore;
using Treasury.Domain.Entities;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Persistence;

public static class ApprovalPolicySeeder
{
    public static async Task Seed(
        TreasuryDbContext context)
    {
        await AddIfMissing(
            context,
            ApprovalOperationTypes
                .InternalTransfer,
            "NGN",
            10_000_000m);

        await AddIfMissing(
            context,
            ApprovalOperationTypes
                .CashPayment,
            "NGN",
            10_000_000m);
        
        await AddIfMissing(
            context,
            ApprovalOperationTypes
                .TransactionReversal,
            "NGN",
            0m);

        await context.SaveChangesAsync();
    }

    private static async Task AddIfMissing(
        TreasuryDbContext context,
        string operationType,
        string currency,
        decimal threshold)
    {
        var exists =
            await context.ApprovalPolicies
                .AnyAsync(policy =>
                    policy.OperationType ==
                        operationType &&
                    policy.Currency ==
                        currency);

        if (exists)
        {
            return;
        }

        await context.ApprovalPolicies.AddAsync(
            new ApprovalPolicy
            {
                Id = Guid.NewGuid(),

                OperationType =
                    operationType,

                Currency =
                    currency,

                ThresholdAmount =
                    threshold,
                
                RequiredApprovalCount =
                    1,
                
                PendingRequestExpiryHours =
                    24,

                IsActive =
                    true,

                CreatedAtUtc =
                    DateTime.UtcNow,

                UpdatedAtUtc =
                    DateTime.UtcNow,

                ConcurrencyToken =
                    Guid.NewGuid()
            });
    }
}
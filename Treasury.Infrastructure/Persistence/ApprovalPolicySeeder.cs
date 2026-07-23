using Microsoft.EntityFrameworkCore;
using Treasury.Domain.Entities;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Persistence;

public static class ApprovalPolicySeeder
{
    public static async Task Seed(
        TreasuryDbContext context)
    {
        var organizationId =
            await context.Organizations
                .Where(organization =>
                    organization.Code ==
                        OrganizationDefaults
                            .OrganizationCode)
                .Select(organization =>
                    organization.Id)
                .SingleAsync();

        await AddIfMissing(
            context,
            organizationId,
            ApprovalOperationTypes
                .InternalTransfer,
            "NGN",
            10_000_000m);

        await AddIfMissing(
            context,
            organizationId,
            ApprovalOperationTypes
                .CashPayment,
            "NGN",
            10_000_000m);
        
        await AddIfMissing(
            context,
            organizationId,
            ApprovalOperationTypes
                .TransactionReversal,
            "NGN",
            0m);
        
        await AddIfMissing(
            context,
            organizationId,
            ApprovalOperationTypes
                .InvestmentPlacement,
            "NGN",
            0m);

        await AddIfMissing(
            context,
            organizationId,
            ApprovalOperationTypes
                .InvestmentEarlyRedemption,
            "NGN",
            0m);
        
        await AddIfMissing(
            context,
            organizationId,
            ApprovalOperationTypes
                .InvestmentRollover,
            "NGN",
            0m);
        
        await AddIfMissing(
            context,
            organizationId,
            ApprovalOperationTypes
                .CreditFacilityActivation,
            "NGN",
            0m);

        await context.SaveChangesAsync();
    }

    private static async Task AddIfMissing(
        TreasuryDbContext context,
        Guid organizationId,
        string operationType,
        string currency,
        decimal threshold)
    {
        var exists =
            await context.ApprovalPolicies
                .AnyAsync(policy =>
                    policy.OrganizationId ==
                        organizationId &&
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

                OrganizationId =
                    organizationId,

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

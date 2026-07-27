using Microsoft.EntityFrameworkCore;
using Treasury.Infrastructure.Persistence;
using Treasury.Shared.Constants;

namespace Treasury.Tests.Integration;

public class ApprovalPolicySeederIntegrationTests
{
    [Fact]
    public async Task
        SeederAcceptsEveryOperationTypeAndIsIdempotent()
    {
        await using var database =
            await PostgreSqlTestDatabase.Start();

        await using var context =
            database.CreateSystemContext();

        await ApprovalPolicySeeder.Seed(context);
        await ApprovalPolicySeeder.Seed(context);

        var operationTypes =
            await context.ApprovalPolicies
                .AsNoTracking()
                .OrderBy(item =>
                    item.OperationType)
                .Select(item =>
                    item.OperationType)
                .ToListAsync();

        Assert.Equal(
            7,
            operationTypes.Count);
        Assert.Contains(
            ApprovalOperationTypes
                .InternalTransfer,
            operationTypes);
        Assert.Contains(
            ApprovalOperationTypes
                .CashPayment,
            operationTypes);
        Assert.Contains(
            ApprovalOperationTypes
                .TransactionReversal,
            operationTypes);
        Assert.Contains(
            ApprovalOperationTypes
                .InvestmentPlacement,
            operationTypes);
        Assert.Contains(
            ApprovalOperationTypes
                .InvestmentEarlyRedemption,
            operationTypes);
        Assert.Contains(
            ApprovalOperationTypes
                .InvestmentRollover,
            operationTypes);
        Assert.Contains(
            ApprovalOperationTypes
                .CreditFacilityActivation,
            operationTypes);
    }
}

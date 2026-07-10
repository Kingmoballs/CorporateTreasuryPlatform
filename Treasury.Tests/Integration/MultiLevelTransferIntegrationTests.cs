using Microsoft.EntityFrameworkCore;
using Moq;
using Treasury.Application.DTOs.ApprovalPolicies;
using Treasury.Application.DTOs.Transfers;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;
using Treasury.Infrastructure.Repositories;
using Treasury.Infrastructure.Services;
using Treasury.Shared.Constants;

namespace Treasury.Tests.Integration;

public class MultiLevelTransferIntegrationTests
{
    [Fact]
    public async Task
        LargeTransfer_TwoApprovals_ExecutesOnlyAfterFinalApproval()
    {
        // Arrange
        await using var database =
            await PostgreSqlTestDatabase.Start();

        var seeded =
            await SeedRequiredData(database);

        const decimal transferAmount =
            12_000_000m;

        Guid transferRequestId;

        // Submit a transfer requiring two approvals.
        await using (
            var requestContext =
                database.CreateContext())
        {
            var service =
                CreateTransferService(
                    requestContext,
                    seeded.RequesterId);

            var response =
                await service.TransferFunds(
                    new CreateTransferDto
                    {
                        FromAccountId =
                            seeded.SourceAccountId,

                        ToAccountId =
                            seeded.DestinationAccountId,

                        Amount =
                            transferAmount,

                        Description =
                            "Two-level transfer test"
                    });

            Assert.Equal(
                ApprovalStatus.Pending,
                response.Status);

            Assert.Null(
                response.TransactionId);

            Assert.Equal(
                0,
                response.ApprovalCount);

            Assert.Equal(
                2,
                response.RequiredApprovalCount);
        }

        // Verify that submission reserved funds.
        await using (
            var reservationContext =
                database.CreateContext())
        {
            var request =
                await reservationContext
                    .TransferRequests
                    .AsNoTracking()
                    .SingleAsync();

            var sourceAccount =
                await reservationContext.Accounts
                    .AsNoTracking()
                    .SingleAsync(account =>
                        account.Id ==
                            seeded.SourceAccountId);

            var destinationAccount =
                await reservationContext.Accounts
                    .AsNoTracking()
                    .SingleAsync(account =>
                        account.Id ==
                            seeded.DestinationAccountId);

            transferRequestId =
                request.Id;

            Assert.Equal(
                ApprovalStatus.Pending,
                request.Status);

            Assert.Equal(
                0,
                request.ApprovalCount);

            Assert.Equal(
                2,
                request.RequiredApprovalCount);

            Assert.Equal(
                20_000_000m,
                sourceAccount.Balance);

            Assert.Equal(
                transferAmount,
                sourceAccount.ReservedBalance);

            Assert.Equal(
                5_000_000m,
                destinationAccount.Balance);
        }

        // First approval must not move the money.
        await using (
            var firstApprovalContext =
                database.CreateContext())
        {
            var service =
                CreateTransferService(
                    firstApprovalContext,
                    seeded.FirstApproverId);

            var response =
                await service.ApproveTransfer(
                    transferRequestId);

            Assert.Equal(
                ApprovalStatus.Pending,
                response.Status);

            Assert.Null(
                response.TransactionId);

            Assert.Equal(
                1,
                response.ApprovalCount);

            Assert.Equal(
                2,
                response.RequiredApprovalCount);
        }

        // Confirm the first approval only recorded a decision.
        await using (
            var intermediateContext =
                database.CreateContext())
        {
            var request =
                await intermediateContext
                    .TransferRequests
                    .AsNoTracking()
                    .SingleAsync(item =>
                        item.Id ==
                            transferRequestId);

            var sourceAccount =
                await intermediateContext.Accounts
                    .AsNoTracking()
                    .SingleAsync(account =>
                        account.Id ==
                            seeded.SourceAccountId);

            var transactionCount =
                await intermediateContext
                    .TreasuryTransactions
                    .CountAsync(transaction =>
                        transaction.TransferRequestId ==
                            transferRequestId);

            var decisions =
                await intermediateContext
                    .ApprovalDecisions
                    .AsNoTracking()
                    .Where(decision =>
                        decision.TransferRequestId ==
                            transferRequestId)
                    .ToListAsync();

            Assert.Equal(
                ApprovalStatus.Pending,
                request.Status);

            Assert.Equal(
                1,
                request.ApprovalCount);

            Assert.Equal(
                20_000_000m,
                sourceAccount.Balance);

            Assert.Equal(
                transferAmount,
                sourceAccount.ReservedBalance);

            Assert.Equal(
                0,
                transactionCount);

            Assert.Single(decisions);

            Assert.Equal(
                seeded.FirstApproverId,
                decisions[0].ApproverUserId);
        }

        // Second distinct approver completes the transfer.
        await using (
            var finalApprovalContext =
                database.CreateContext())
        {
            var service =
                CreateTransferService(
                    finalApprovalContext,
                    seeded.SecondApproverId);

            var response =
                await service.ApproveTransfer(
                    transferRequestId);

            Assert.Equal(
                TransactionStatuses.Completed,
                response.Status);

            Assert.NotNull(
                response.TransactionId);

            Assert.Equal(
                2,
                response.ApprovalCount);

            Assert.Equal(
                2,
                response.RequiredApprovalCount);
        }

        // Assert the final database state.
        await using var verificationContext =
            database.CreateContext();

        var finalRequest =
            await verificationContext
                .TransferRequests
                .AsNoTracking()
                .SingleAsync(request =>
                    request.Id ==
                        transferRequestId);

        var finalSourceAccount =
            await verificationContext.Accounts
                .AsNoTracking()
                .SingleAsync(account =>
                    account.Id ==
                        seeded.SourceAccountId);

        var finalDestinationAccount =
            await verificationContext.Accounts
                .AsNoTracking()
                .SingleAsync(account =>
                    account.Id ==
                        seeded.DestinationAccountId);

        var transactions =
            await verificationContext
                .TreasuryTransactions
                .AsNoTracking()
                .Where(transaction =>
                    transaction.TransferRequestId ==
                        transferRequestId)
                .ToListAsync();

        var decisionsAfterApproval =
            await verificationContext
                .ApprovalDecisions
                .AsNoTracking()
                .Where(decision =>
                    decision.TransferRequestId ==
                        transferRequestId)
                .ToListAsync();

        Assert.Equal(
            ApprovalStatus.Approved,
            finalRequest.Status);

        Assert.Equal(
            2,
            finalRequest.ApprovalCount);

        Assert.Equal(
            seeded.SecondApproverId,
            finalRequest.ReviewedByUserId);

        Assert.Equal(
            8_000_000m,
            finalSourceAccount.Balance);

        Assert.Equal(
            0m,
            finalSourceAccount.ReservedBalance);

        Assert.Equal(
            17_000_000m,
            finalDestinationAccount.Balance);

        var transaction =
            Assert.Single(transactions);

        Assert.Equal(
            TransactionTypes.InternalTransfer,
            transaction.TransactionType);

        Assert.Equal(
            transferAmount,
            transaction.Amount);

        Assert.Equal(
            2,
            decisionsAfterApproval.Count);

        Assert.Contains(
            decisionsAfterApproval,
            decision =>
                decision.ApproverUserId ==
                    seeded.FirstApproverId);

        Assert.Contains(
            decisionsAfterApproval,
            decision =>
                decision.ApproverUserId ==
                    seeded.SecondApproverId);
    }

    private static TransferService
        CreateTransferService(
            TreasuryDbContext context,
            Guid userId)
    {
        var currentUser =
            new Mock<ICurrentUserService>();

        currentUser
            .SetupGet(service =>
                service.UserId)
            .Returns(userId);

        var approvalPolicyService =
            new Mock<IApprovalPolicyService>();

        approvalPolicyService
            .Setup(service =>
                service.GetRequirements(
                    It.IsAny<string>(),
                    It.IsAny<string>()))
            .ReturnsAsync(
                new ApprovalRequirementsDto
                {
                    ThresholdAmount =
                        10_000_000m,

                    RequiredApprovalCount =
                        2
                });

        return new TransferService(
            new AccountRepository(context),
            new LedgerRepository(context),
            new TransferRequestRepository(context),
            currentUser.Object,
            new TreasuryTransactionRepository(context),
            approvalPolicyService.Object,
            new ApprovalDecisionRepository(context),
            new AuditLogService(
                new AuditLogRepository(context),
                currentUser.Object)
            );
    }

    private static async Task<SeededData>
        SeedRequiredData(
            PostgreSqlTestDatabase database)
    {
        await using var context =
            database.CreateContext();

        var officerRole = new Role
        {
            Id = Guid.NewGuid(),
            Name = Roles.TreasuryOfficer
        };

        var managerRole = new Role
        {
            Id = Guid.NewGuid(),
            Name = Roles.FinanceManager
        };

        var requester =
            CreateUser(
                officerRole,
                "transfer-requester");

        var firstApprover =
            CreateUser(
                managerRole,
                "transfer-first-approver");

        var secondApprover =
            CreateUser(
                managerRole,
                "transfer-second-approver");

        var accountType = new AccountType
        {
            Id = Guid.NewGuid(),
            Name = AccountTypes.Operating
        };

        var sourceAccount = new Account
        {
            Id = Guid.NewGuid(),

            Name =
                "Multi-Level Source Account",

            AccountNumber =
                $"MULTI-SOURCE-{Guid.NewGuid():N}",

            Balance =
                20_000_000m,

            ReservedBalance =
                0m,

            Currency =
                "NGN",

            IsActive =
                true,

            AccountTypeId =
                accountType.Id,

            AccountType =
                accountType,

            ConcurrencyToken =
                Guid.NewGuid(),

            CreatedAt =
                DateTime.UtcNow
        };

        var destinationAccount = new Account
        {
            Id = Guid.NewGuid(),

            Name =
                "Multi-Level Destination Account",

            AccountNumber =
                $"MULTI-DEST-{Guid.NewGuid():N}",

            Balance =
                5_000_000m,

            ReservedBalance =
                0m,

            Currency =
                "NGN",

            IsActive =
                true,

            AccountTypeId =
                accountType.Id,

            AccountType =
                accountType,

            ConcurrencyToken =
                Guid.NewGuid(),

            CreatedAt =
                DateTime.UtcNow
        };

        await context.Roles.AddRangeAsync(
            officerRole,
            managerRole);

        await context.Users.AddRangeAsync(
            requester,
            firstApprover,
            secondApprover);

        await context.AccountTypes.AddAsync(
            accountType);

        await context.Accounts.AddRangeAsync(
            sourceAccount,
            destinationAccount);

        await context.SaveChangesAsync();

        return new SeededData(
            requester.Id,
            firstApprover.Id,
            secondApprover.Id,
            sourceAccount.Id,
            destinationAccount.Id);
    }

    private static User CreateUser(
        Role role,
        string prefix)
    {
        return new User
        {
            Id = Guid.NewGuid(),

            FirstName =
                prefix,

            LastName =
                "Tester",

            Email =
                $"{prefix}-{Guid.NewGuid():N}" +
                "@example.com",

            PasswordHash =
                "not-used",

            RoleId =
                role.Id,

            Role =
                role,

            IsActive =
                true,

            CreatedAt =
                DateTime.UtcNow
        };
    }

    private sealed record SeededData(
        Guid RequesterId,
        Guid FirstApproverId,
        Guid SecondApproverId,
        Guid SourceAccountId,
        Guid DestinationAccountId);
}
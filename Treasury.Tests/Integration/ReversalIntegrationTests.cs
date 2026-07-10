using Microsoft.EntityFrameworkCore;
using Moq;
using Treasury.Application.DTOs.CashMovements;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;
using Treasury.Infrastructure.Repositories;
using Treasury.Infrastructure.Services;
using Treasury.Shared.Constants;
using Treasury.Application.DTOs.ApprovalPolicies;

namespace Treasury.Tests.Integration;

public class ReversalIntegrationTests
{
    [Fact]
    public async Task ApprovedReceiptReversal_RestoresOriginalBalance()
    {
        await using var database =
            await PostgreSqlTestDatabase.Start();

        SeededData seededData;

        await using (
            var seedContext =
                database.CreateContext())
        {
            seededData =
                await SeedRequiredData(
                    seedContext);
        }

        string receiptReference;
        Guid receiptTransactionId;

        // Record the original receipt as the maker.
        await using (
            var receiptContext =
                database.CreateContext())
        {
            var receiptService =
                CreateCashMovementService(
                    receiptContext,
                    seededData.RequesterId);

            var receipt =
                await receiptService.RecordReceipt(
                    new CreateCashReceiptDto
                    {
                        AccountId =
                            seededData.AccountId,

                        Amount =
                            2_000_000m,

                        CounterpartyName =
                            "Test Customer",

                        Category =
                            "CustomerReceipt",

                        ExternalReference =
                            "REVERSAL-TEST-RECEIPT",

                        IdempotencyKey =
                            "reversal-test-receipt",

                        Description =
                            "Receipt to be reversed"
                    });

            receiptReference =
                receipt.TransactionReference;

            receiptTransactionId =
                receipt.TransactionId;
        }

        Guid reversalRequestId;

        // The same maker requests the reversal.
        await using (
            var requestContext =
                database.CreateContext())
        {
            var reversalService =
                CreateReversalService(
                    requestContext,
                    seededData.RequesterId);

            var request =
                await reversalService
                    .RequestReversal(
                        receiptReference,
                        "Receipt was posted twice.");

            reversalRequestId =
                request.Id;

            Assert.Equal(
                ApprovalStatus.Pending,
                request.Status);
        }

        // A different user approves it.
        await using (
            var approvalContext =
                database.CreateContext())
        {
            var reversalService =
                CreateReversalService(
                    approvalContext,
                    seededData.ApproverId);

            var approval =
                await reversalService
                    .Approve(reversalRequestId);

            Assert.NotNull(
                approval.Transaction);

            var reversal =
                approval.Transaction!;

            Assert.Equal(
                TransactionTypes.Reversal,
                reversal.TransactionType);

            Assert.Equal(
                receiptTransactionId,
                reversal.ReversesTransactionId);

            Assert.Equal(
                receiptReference,
                reversal
                    .ReversesTransactionReference);
        }

        await using var verificationContext =
            database.CreateContext();

        var account =
            await verificationContext.Accounts
                .AsNoTracking()
                .SingleAsync(item =>
                    item.Id ==
                        seededData.AccountId);

        var transactions =
            await verificationContext
                .TreasuryTransactions
                .AsNoTracking()
                .OrderBy(item =>
                    item.CreatedAtUtc)
                .ToListAsync();

        var ledgerEntries =
            await verificationContext
                .LedgerEntries
                .AsNoTracking()
                .OrderBy(entry =>
                    entry.CreatedAt)
                .ToListAsync();

        var requestAfterApproval =
            await verificationContext
                .ReversalRequests
                .AsNoTracking()
                .SingleAsync(request =>
                    request.Id ==
                        reversalRequestId);
        

        /*
         * ₦10M + ₦2M receipt - ₦2M reversal
         * returns the account to its original balance.
         */
        Assert.Equal(
            10_000_000m,
            account.Balance);

        Assert.Equal(
            2,
            transactions.Count);

        Assert.Equal(
            2,
            ledgerEntries.Count);

        Assert.Equal(
            "Debit",
            ledgerEntries[0].EntryType);

        Assert.Equal(
            "Credit",
            ledgerEntries[1].EntryType);

        Assert.Equal(
            ApprovalStatus.Approved,
            requestAfterApproval.Status);

        Assert.Equal(
            seededData.ApproverId,
            requestAfterApproval
                .ReviewedByUserId);
    }

    [Fact]
    public async Task
        ReceiptReversal_TwoApprovals_ReversesOnlyAfterFinalApproval()
    {
        // Arrange
        await using var database =
            await PostgreSqlTestDatabase.Start();

        SeededData seeded;

        await using (
            var seedContext =
                database.CreateContext())
        {
            seeded =
                await SeedRequiredData(
                    seedContext);
        }

        string receiptReference;

        // Record the original ₦2M receipt.
        await using (
            var receiptContext =
                database.CreateContext())
        {
            var service =
                CreateCashMovementService(
                    receiptContext,
                    seeded.RequesterId);

            var receipt =
                await service.RecordReceipt(
                    new CreateCashReceiptDto
                    {
                        AccountId =
                            seeded.AccountId,

                        Amount =
                            2_000_000m,

                        CounterpartyName =
                            "Multi-Level Test Customer",

                        Category =
                            "CustomerReceipt",

                        ExternalReference =
                            "MULTI-REVERSAL-RECEIPT",

                        IdempotencyKey =
                            "multi-reversal-receipt-001",

                        Description =
                            "Receipt requiring reversal"
                    });

            receiptReference =
                receipt.TransactionReference;
        }

        Guid reversalRequestId;

        // Request a reversal requiring two approvals.
        await using (
            var requestContext =
                database.CreateContext())
        {
            var service =
                CreateReversalService(
                    requestContext,
                    seeded.RequesterId,
                    requiredApprovalCount: 2);

            var response =
                await service.RequestReversal(
                    receiptReference,
                    "Receipt was posted twice.");

            reversalRequestId =
                response.Id;

            Assert.Equal(
                ApprovalStatus.Pending,
                response.Status);

            Assert.Equal(
                0,
                response.ApprovalCount);

            Assert.Equal(
                2,
                response.RequiredApprovalCount);
        }

        // First approval must not reverse the receipt.
        await using (
            var firstApprovalContext =
                database.CreateContext())
        {
            var service =
                CreateReversalService(
                    firstApprovalContext,
                    seeded.ApproverId,
                    requiredApprovalCount: 2);

            var response =
                await service.Approve(
                    reversalRequestId);

            Assert.Null(
                response.Transaction);

            Assert.Equal(
                ApprovalStatus.Pending,
                response.Request.Status);

            Assert.Equal(
                1,
                response.Request.ApprovalCount);

            Assert.Equal(
                2,
                response.Request.RequiredApprovalCount);
        }

        // Verify that the first approval changed no balance.
        await using (
            var intermediateContext =
                database.CreateContext())
        {
            var account =
                await intermediateContext.Accounts
                    .AsNoTracking()
                    .SingleAsync(item =>
                        item.Id == seeded.AccountId);

            var request =
                await intermediateContext
                    .ReversalRequests
                    .AsNoTracking()
                    .SingleAsync(item =>
                        item.Id ==
                            reversalRequestId);

            var reversalTransactionCount =
                await intermediateContext
                    .TreasuryTransactions
                    .CountAsync(transaction =>
                        transaction.ReversalRequestId ==
                            reversalRequestId);

            var decisions =
                await intermediateContext
                    .ApprovalDecisions
                    .AsNoTracking()
                    .Where(decision =>
                        decision.ReversalRequestId ==
                            reversalRequestId)
                    .ToListAsync();

            // ₦10M opening balance + ₦2M receipt.
            Assert.Equal(
                12_000_000m,
                account.Balance);

            Assert.Equal(
                ApprovalStatus.Pending,
                request.Status);

            Assert.Equal(
                1,
                request.ApprovalCount);

            Assert.Equal(
                0,
                reversalTransactionCount);

            Assert.Single(decisions);

            Assert.Equal(
                seeded.ApproverId,
                decisions[0].ApproverUserId);
        }

        // Second distinct approver executes the reversal.
        await using (
            var secondApprovalContext =
                database.CreateContext())
        {
            var service =
                CreateReversalService(
                    secondApprovalContext,
                    seeded.SecondApproverId,
                    requiredApprovalCount: 2);

            var response =
                await service.Approve(
                    reversalRequestId);

            Assert.NotNull(
                response.Transaction);

            Assert.Equal(
                ApprovalStatus.Approved,
                response.Request.Status);

            Assert.Equal(
                2,
                response.Request.ApprovalCount);

            Assert.Equal(
                TransactionTypes.Reversal,
                response.Transaction!.TransactionType);
        }

        // Assert the final database state.
        await using var verificationContext =
            database.CreateContext();

        var finalAccount =
            await verificationContext.Accounts
                .AsNoTracking()
                .SingleAsync(item =>
                    item.Id == seeded.AccountId);

        var finalRequest =
            await verificationContext
                .ReversalRequests
                .AsNoTracking()
                .SingleAsync(item =>
                    item.Id == reversalRequestId);

        var reversalTransactions =
            await verificationContext
                .TreasuryTransactions
                .AsNoTracking()
                .Where(transaction =>
                    transaction.ReversalRequestId ==
                        reversalRequestId)
                .ToListAsync();

        var finalDecisions =
            await verificationContext
                .ApprovalDecisions
                .AsNoTracking()
                .Where(decision =>
                    decision.ReversalRequestId ==
                        reversalRequestId)
                .ToListAsync();

        // The reversal returns the account to ₦10M.
        Assert.Equal(
            10_000_000m,
            finalAccount.Balance);

        Assert.Equal(
            ApprovalStatus.Approved,
            finalRequest.Status);

        Assert.Equal(
            2,
            finalRequest.ApprovalCount);

        Assert.Equal(
            2,
            finalRequest.RequiredApprovalCount);

        Assert.Equal(
            seeded.SecondApproverId,
            finalRequest.ReviewedByUserId);

        var reversalTransaction =
            Assert.Single(reversalTransactions);

        Assert.Equal(
            TransactionTypes.Reversal,
            reversalTransaction.TransactionType);

        Assert.Equal(
            2_000_000m,
            reversalTransaction.Amount);

        Assert.Equal(
            2,
            finalDecisions.Count);

        Assert.Contains(
            finalDecisions,
            decision =>
                decision.ApproverUserId ==
                    seeded.ApproverId);

        Assert.Contains(
            finalDecisions,
            decision =>
                decision.ApproverUserId ==
                    seeded.SecondApproverId);
    }

    private static CashMovementService
        CreateCashMovementService(
            TreasuryDbContext context,
            Guid userId)
    {
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
                        1
                });

        return new CashMovementService(
            new AccountRepository(context),
            new LedgerRepository(context),
            new TreasuryTransactionRepository(
                context),
            CreateCurrentUser(userId),
            new PaymentRequestRepository(
                context),
            approvalPolicyService.Object,
            new ApprovalDecisionRepository(context),
            new AuditLogService(
                new AuditLogRepository(context),
                CreateCurrentUser(userId)));
    }

    private static ReversalService
        CreateReversalService(
            TreasuryDbContext context,
            Guid userId,
            int requiredApprovalCount = 1)
    {
        var transactionRepository =
            new TreasuryTransactionRepository(
                context);
        
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
                    ThresholdAmount = 0m,

                    RequiredApprovalCount = 
                        requiredApprovalCount
                });

        return new ReversalService(
            new AccountRepository(context),
            new LedgerRepository(context),
            transactionRepository,
            new ReversalRequestRepository(
                context),
            new TreasuryTransactionService(
                transactionRepository),
            CreateCurrentUser(userId),
            approvalPolicyService.Object,
            new ApprovalDecisionRepository(context),
            new AuditLogService(
                new AuditLogRepository(context),
                CreateCurrentUser(userId)));
    }

    private static ICurrentUserService
        CreateCurrentUser(Guid userId)
    {
        var currentUser =
            new Mock<ICurrentUserService>();

        currentUser
            .SetupGet(service =>
                service.UserId)
            .Returns(userId);

        return currentUser.Object;
    }

    private static async Task<SeededData>
        SeedRequiredData(
            TreasuryDbContext context)
    {
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

        var requester = CreateUser(
            officerRole,
            "requester");

        var approver = CreateUser(
            managerRole,
            "approver");
        
        var secondApprover =
            CreateUser(
                managerRole,
                "second-approver");

        var accountType = new AccountType
        {
            Id = Guid.NewGuid(),
            Name = AccountTypes.Operating
        };

        var account = new Account
        {
            Id = Guid.NewGuid(),

            Name =
                "Reversal Test Account",

            AccountNumber =
                $"REVERSAL-{Guid.NewGuid():N}",

            Balance =
                10_000_000m,

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
            approver,
            secondApprover);

        await context.AccountTypes
            .AddAsync(accountType);

        await context.Accounts
            .AddAsync(account);

        await context.SaveChangesAsync();

        return new SeededData(
            requester.Id,
            approver.Id,
            secondApprover.Id,
            account.Id);
    }

    private static User CreateUser(
        Role role,
        string prefix)
    {
        return new User
        {
            Id = Guid.NewGuid(),

            FirstName = prefix,

            LastName = "Tester",

            Email =
                $"{prefix}-{Guid.NewGuid():N}" +
                "@example.com",

            PasswordHash = "not-used",

            RoleId = role.Id,

            Role = role,

            IsActive = true,

            CreatedAt = DateTime.UtcNow
        };
    }

    private sealed record SeededData(
        Guid RequesterId,
        Guid ApproverId,
        Guid SecondApproverId,
        Guid AccountId);
}
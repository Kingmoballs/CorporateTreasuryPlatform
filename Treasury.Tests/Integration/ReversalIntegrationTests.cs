using Microsoft.EntityFrameworkCore;
using Moq;
using Treasury.Application.DTOs.CashMovements;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;
using Treasury.Infrastructure.Repositories;
using Treasury.Infrastructure.Services;
using Treasury.Shared.Constants;

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

            var reversal =
                await reversalService
                    .Approve(
                        reversalRequestId);

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

    private static CashMovementService
        CreateCashMovementService(
            TreasuryDbContext context,
            Guid userId)
    {
        var approvalPolicyService =
            new Mock<IApprovalPolicyService>();

        approvalPolicyService
            .Setup(service =>
                service.GetThreshold(
                    It.IsAny<string>(),
                    It.IsAny<string>()))
            .ReturnsAsync(10_000_000m);

        return new CashMovementService(
            new AccountRepository(context),
            new LedgerRepository(context),
            new TreasuryTransactionRepository(
                context),
            CreateCurrentUser(userId),
            new PaymentRequestRepository(
                context),
            approvalPolicyService.Object);
    }

    private static ReversalService
        CreateReversalService(
            TreasuryDbContext context,
            Guid userId)
    {
        var transactionRepository =
            new TreasuryTransactionRepository(
                context);

        return new ReversalService(
            new AccountRepository(context),
            new LedgerRepository(context),
            transactionRepository,
            new ReversalRequestRepository(
                context),
            new TreasuryTransactionService(
                transactionRepository),
            CreateCurrentUser(userId));
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
            approver);

        await context.AccountTypes
            .AddAsync(accountType);

        await context.Accounts
            .AddAsync(account);

        await context.SaveChangesAsync();

        return new SeededData(
            requester.Id,
            approver.Id,
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
        Guid AccountId);
}
using Microsoft.EntityFrameworkCore;
using Moq;
using Treasury.Application.DTOs.Approvals;
using Treasury.Application.DTOs.ApprovalPolicies;
using Treasury.Application.DTOs.CashMovements;
using Treasury.Application.DTOs.Transfers;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;
using Treasury.Infrastructure.Repositories;
using Treasury.Infrastructure.Services;
using Treasury.Shared.Constants;

namespace Treasury.Tests.Integration;

public class PendingRequestExpiryIntegrationTests
{
    [Fact]
    public async Task ExpiredTransfer_ReleasesReservationAndDoesNotMoveCash()
    {
        // Arrange
        await using var database =
            await PostgreSqlTestDatabase.Start();

        var seeded =
            await SeedRequiredData(database);

        const decimal transferAmount =
            12_000_000m;

        Guid transferRequestId;

        await using (
            var requestContext =
                database.CreateContext())
        {
            var transferService =
                CreateTransferService(
                    requestContext,
                    seeded.RequesterId);

            var response =
                await transferService.TransferFunds(
                    new CreateTransferDto
                    {
                        FromAccountId =
                            seeded.SourceAccountId,

                        ToAccountId =
                            seeded.DestinationAccountId,

                        Amount =
                            transferAmount,

                        Description =
                            "Expired transfer reservation test"
                    });

            Assert.Equal(
                ApprovalStatus.Pending,
                response.Status);

            Assert.Null(
                response.TransactionId);

            var request =
                await requestContext.TransferRequests
                    .SingleAsync();

            transferRequestId =
                request.Id;

            /*
             * We force the request into the past so the
             * expiry service can process it immediately.
             */
            request.ExpiresAtUtc =
                DateTime.UtcNow.AddMinutes(-5);

            await requestContext.SaveChangesAsync();
        }

        // Act
        PendingRequestExpiryResultDto result;

        await using (
            var expiryContext =
                database.CreateContext())
        {
            var expiryService =
                new PendingRequestExpiryService(
                    expiryContext);

            result =
                await expiryService.ExpireDueRequests();
        }

        // Assert
        await using var verificationContext =
            database.CreateContext();

        var expiredRequest =
            await verificationContext.TransferRequests
                .AsNoTracking()
                .SingleAsync(request =>
                    request.Id == transferRequestId);

        var sourceAccount =
            await verificationContext.Accounts
                .AsNoTracking()
                .SingleAsync(account =>
                    account.Id == seeded.SourceAccountId);

        var destinationAccount =
            await verificationContext.Accounts
                .AsNoTracking()
                .SingleAsync(account =>
                    account.Id == seeded.DestinationAccountId);

        var transactions =
            await verificationContext.TreasuryTransactions
                .AsNoTracking()
                .Where(transaction =>
                    transaction.TransferRequestId ==
                    transferRequestId)
                .ToListAsync();

        Assert.Equal(
            1,
            result.ExpiredTransferCount);

        Assert.Equal(
            0,
            result.ExpiredPaymentCount);

        Assert.Equal(
            0,
            result.ExpiredReversalCount);

        Assert.Equal(
            ApprovalStatus.Expired,
            expiredRequest.Status);

        Assert.Equal(
            20_000_000m,
            sourceAccount.Balance);

        Assert.Equal(
            0m,
            sourceAccount.ReservedBalance);

        Assert.Equal(
            5_000_000m,
            destinationAccount.Balance);

        Assert.Empty(
            transactions);
    }

    [Fact]
    public async Task ExpiredPayment_ReleasesReservationAndDoesNotCreateTransaction()
    {
        // Arrange
        await using var database =
            await PostgreSqlTestDatabase.Start();

        var seeded =
            await SeedRequiredData(database);

        const decimal paymentAmount =
            12_000_000m;

        Guid paymentRequestId;

        await using (
            var requestContext =
                database.CreateContext())
        {
            var cashMovementService =
                CreateCashMovementService(
                    requestContext,
                    seeded.RequesterId);

            var response =
                await cashMovementService.RecordPayment(
                    CreatePayment(
                        seeded.SourceAccountId,
                        paymentAmount,
                        $"EXP-PAY-{Guid.NewGuid():N}"));

            Assert.Equal(
                ApprovalStatus.Pending,
                response.Status);

            Assert.Null(
                response.TransactionId);

            paymentRequestId =
                response.PaymentRequestId
                ?? throw new InvalidOperationException(
                    "Payment request id was not returned.");

            var request =
                await requestContext.PaymentRequests
                    .SingleAsync(request =>
                        request.Id == paymentRequestId);

            /*
             * The request was created as pending.
             * Moving ExpiresAtUtc into the past makes it due.
             */
            request.ExpiresAtUtc =
                DateTime.UtcNow.AddMinutes(-5);

            await requestContext.SaveChangesAsync();
        }

        // Act
        PendingRequestExpiryResultDto result;

        await using (
            var expiryContext =
                database.CreateContext())
        {
            var expiryService =
                new PendingRequestExpiryService(
                    expiryContext);

            result =
                await expiryService.ExpireDueRequests();
        }

        // Assert
        await using var verificationContext =
            database.CreateContext();

        var expiredRequest =
            await verificationContext.PaymentRequests
                .AsNoTracking()
                .SingleAsync(request =>
                    request.Id == paymentRequestId);

        var account =
            await verificationContext.Accounts
                .AsNoTracking()
                .SingleAsync(account =>
                    account.Id == seeded.SourceAccountId);

        var transactions =
            await verificationContext.TreasuryTransactions
                .AsNoTracking()
                .Where(transaction =>
                    transaction.PaymentRequestId ==
                    paymentRequestId)
                .ToListAsync();

        Assert.Equal(
            0,
            result.ExpiredTransferCount);

        Assert.Equal(
            1,
            result.ExpiredPaymentCount);

        Assert.Equal(
            0,
            result.ExpiredReversalCount);

        Assert.Equal(
            ApprovalStatus.Expired,
            expiredRequest.Status);

        Assert.Equal(
            20_000_000m,
            account.Balance);

        Assert.Equal(
            0m,
            account.ReservedBalance);

        Assert.Empty(
            transactions);
    }

    [Fact]
    public async Task ExpiredReversal_ExpiresWithoutChangingAccountBalance()
    {
        // Arrange
        await using var database =
            await PostgreSqlTestDatabase.Start();

        var seeded =
            await SeedRequiredData(database);

        const decimal receiptAmount =
            12_000_000m;

        Guid reversalRequestId;

        await using (
            var requestContext =
                database.CreateContext())
        {
            var cashMovementService =
                CreateCashMovementService(
                    requestContext,
                    seeded.RequesterId);

            var receipt =
                await cashMovementService.RecordReceipt(
                    CreateReceipt(
                        seeded.SourceAccountId,
                        receiptAmount,
                        $"EXP-REC-{Guid.NewGuid():N}"));

            var reversalService =
                CreateReversalService(
                    requestContext,
                    seeded.RequesterId);

            var reversalResponse =
                await reversalService.RequestReversal(
                    receipt.TransactionReference!,
                    "Expired reversal integration test");

            Assert.Equal(
                ApprovalStatus.Pending,
                reversalResponse.Status);

            reversalRequestId =
                reversalResponse.Id;

            var request =
                await requestContext.ReversalRequests
                    .SingleAsync(request =>
                        request.Id == reversalRequestId);

            /*
             * Reversal requests do not reserve funds.
             * Expiring them should only change the request status.
             */
            request.ExpiresAtUtc =
                DateTime.UtcNow.AddMinutes(-5);

            await requestContext.SaveChangesAsync();
        }

        // Act
        PendingRequestExpiryResultDto result;

        await using (
            var expiryContext =
                database.CreateContext())
        {
            var expiryService =
                new PendingRequestExpiryService(
                    expiryContext);

            result =
                await expiryService.ExpireDueRequests();
        }

        // Assert
        await using var verificationContext =
            database.CreateContext();

        var expiredRequest =
            await verificationContext.ReversalRequests
                .AsNoTracking()
                .SingleAsync(request =>
                    request.Id == reversalRequestId);

        var account =
            await verificationContext.Accounts
                .AsNoTracking()
                .SingleAsync(account =>
                    account.Id == seeded.SourceAccountId);

        var reversalTransactions =
            await verificationContext.TreasuryTransactions
                .AsNoTracking()
                .Where(transaction =>
                    transaction.ReversalRequestId ==
                    reversalRequestId)
                .ToListAsync();

        Assert.Equal(
            0,
            result.ExpiredTransferCount);

        Assert.Equal(
            0,
            result.ExpiredPaymentCount);

        Assert.Equal(
            1,
            result.ExpiredReversalCount);

        Assert.Equal(
            ApprovalStatus.Expired,
            expiredRequest.Status);

        Assert.Equal(
            32_000_000m,
            account.Balance);

        Assert.Equal(
            0m,
            account.ReservedBalance);

        Assert.Empty(
            reversalTransactions);
    }

    private static TransferService CreateTransferService(
        TreasuryDbContext context,
        Guid userId)
    {
        return new TransferService(
            new AccountRepository(context),
            new LedgerRepository(context),
            new TransferRequestRepository(context),
            CreateCurrentUser(userId),
            new TreasuryTransactionRepository(context),
            CreateApprovalPolicyService(),
            new ApprovalDecisionRepository(context),
            new AuditLogService(
                new AuditLogRepository(context),
                CreateCurrentUser(userId)));
    }

    private static CashMovementService CreateCashMovementService(
        TreasuryDbContext context,
        Guid userId)
    {
        return new CashMovementService(
            new AccountRepository(context),
            new LedgerRepository(context),
            new TreasuryTransactionRepository(context),
            CreateCurrentUser(userId),
            new PaymentRequestRepository(context),
            CreateApprovalPolicyService(),
            new ApprovalDecisionRepository(context),
            new AuditLogService(
                new AuditLogRepository(context),
                CreateCurrentUser(userId)));
    }

    private static ReversalService CreateReversalService(
        TreasuryDbContext context,
        Guid userId)
    {
        var transactionRepository =
            new TreasuryTransactionRepository(context);

        return new ReversalService(
            new AccountRepository(context),
            new LedgerRepository(context),
            transactionRepository,
            new ReversalRequestRepository(context),
            new TreasuryTransactionService(transactionRepository),
            CreateCurrentUser(userId),
            CreateApprovalPolicyService(),
            new ApprovalDecisionRepository(context),
            new AuditLogService(
                new AuditLogRepository(context),
                CreateCurrentUser(userId)));
    }

    private static ICurrentUserService CreateCurrentUser(
        Guid userId)
    {
        var currentUser =
            new Mock<ICurrentUserService>();

        currentUser
            .SetupGet(service =>
                service.UserId)
            .Returns(userId);

        return currentUser.Object;
    }

    private static IApprovalPolicyService CreateApprovalPolicyService()
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
                        1,

                    PendingRequestExpiryHours =
                        1
                });

        return approvalPolicyService.Object;
    }

    private static CreateCashPaymentDto CreatePayment(
        Guid accountId,
        decimal amount,
        string idempotencyKey)
    {
        return new CreateCashPaymentDto
        {
            AccountId =
                accountId,

            Amount =
                amount,

            BeneficiaryName =
                "Expiry Test Supplier",

            Category =
                "SupplierPayment",

            ExternalReference =
                idempotencyKey,

            IdempotencyKey =
                idempotencyKey,

            Description =
                "Pending request expiry payment test"
        };
    }

    private static CreateCashReceiptDto CreateReceipt(
        Guid accountId,
        decimal amount,
        string idempotencyKey)
    {
        return new CreateCashReceiptDto
        {
            AccountId =
                accountId,

            Amount =
                amount,

            CounterpartyName =
                "Expiry Test Customer",

            Category =
                "CustomerReceipt",

            ExternalReference =
                idempotencyKey,

            IdempotencyKey =
                idempotencyKey,

            Description =
                "Pending request expiry receipt test"
        };
    }

    private static async Task<SeededData> SeedRequiredData(
        PostgreSqlTestDatabase database)
    {
        await using var context =
            database.CreateContext();

        var officerRole =
            new Role
            {
                Id =
                    Guid.NewGuid(),

                Name =
                    Roles.TreasuryOfficer
            };

        var requester =
            CreateUser(
                officerRole,
                "expiry-requester");

        var accountType =
            new AccountType
            {
                Id =
                    Guid.NewGuid(),

                Name =
                    AccountTypes.Operating
            };

        var sourceAccount =
            new Account
            {
                Id =
                    Guid.NewGuid(),

                Name =
                    "Expiry Source Account",

                AccountNumber =
                    $"EXP-SRC-{Guid.NewGuid():N}",

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

        var destinationAccount =
            new Account
            {
                Id =
                    Guid.NewGuid(),

                Name =
                    "Expiry Destination Account",

                AccountNumber =
                    $"EXP-DST-{Guid.NewGuid():N}",

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

        await context.Roles.AddAsync(
            officerRole);

        await context.Users.AddAsync(
            requester);

        await context.AccountTypes.AddAsync(
            accountType);

        await context.Accounts.AddRangeAsync(
            sourceAccount,
            destinationAccount);

        await context.SaveChangesAsync();

        return new SeededData(
            requester.Id,
            sourceAccount.Id,
            destinationAccount.Id);
    }

    private static User CreateUser(
        Role role,
        string prefix)
    {
        return new User
        {
            Id =
                Guid.NewGuid(),

            FirstName =
                prefix,

            LastName =
                "Tester",

            Email =
                $"{prefix}-{Guid.NewGuid():N}@example.com",

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
        Guid SourceAccountId,
        Guid DestinationAccountId);
}
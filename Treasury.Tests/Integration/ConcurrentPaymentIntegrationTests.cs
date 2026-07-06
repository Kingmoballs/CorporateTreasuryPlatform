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

public class ConcurrentPaymentIntegrationTests
{
    [Fact]
    public async Task SimultaneousPayments_CannotOverspendAccount()
    {
        // Arrange
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

        /*
         * Separate contexts simulate two independent
         * HTTP requests reaching the API together.
         */
        await using var firstContext =
            database.CreateContext();

        await using var secondContext =
            database.CreateContext();

        var barrier =
            new AsyncTestBarrier(2);

        var firstService =
            CreateService(
                firstContext,
                barrier,
                seededData.UserId);

        var secondService =
            CreateService(
                secondContext,
                barrier,
                seededData.UserId);

        /*
         * Each ₦7M payment is individually valid against
         * ₦10M, but both cannot succeed together.
         */
        var firstTask =
            CaptureAttempt(() =>
                firstService.RecordPayment(
                    CreatePayment(
                        seededData.AccountId,
                        "concurrent-payment-001")));

        var secondTask =
            CaptureAttempt(() =>
                secondService.RecordPayment(
                    CreatePayment(
                        seededData.AccountId,
                        "concurrent-payment-002")));

        // Act
        var attempts =
            await Task.WhenAll(
                firstTask,
                secondTask);

        // Assert
        var successfulAttempt =
            Assert.Single(
                attempts.Where(
                    attempt =>
                        attempt.Succeeded));

        var failedAttempt =
            Assert.Single(
                attempts.Where(
                    attempt =>
                        !attempt.Succeeded));

        Assert.NotNull(
            successfulAttempt.Response);

        Assert.NotNull(
            failedAttempt.Exception);

        Assert.Contains(
            "balance changed",
            failedAttempt.Exception!.Message);

        await using var verificationContext =
            database.CreateContext();

        var account =
            await verificationContext.Accounts
                .AsNoTracking()
                .SingleAsync(item =>
                    item.Id ==
                        seededData.AccountId);

        var payments =
            await verificationContext
                .TreasuryTransactions
                .AsNoTracking()
                .Where(transaction =>
                    transaction.TransactionType ==
                        TransactionTypes
                            .CashPayment)
                .ToListAsync();

        var payment =
            Assert.Single(payments);

        var ledgerEntries =
            await verificationContext
                .LedgerEntries
                .AsNoTracking()
                .Where(entry =>
                    entry.TreasuryTransactionId ==
                        payment.Id)
                .ToListAsync();

        var ledgerEntry =
            Assert.Single(ledgerEntries);

        Assert.Equal(
            3_000_000m,
            account.Balance);

        Assert.Equal(
            7_000_000m,
            payment.Amount);

        Assert.Equal(
            "Credit",
            ledgerEntry.EntryType);

        /*
         * The losing request's transaction and ledger
         * entry must have been rolled back completely.
         */
        Assert.Equal(
            payment.Id,
            ledgerEntry.TreasuryTransactionId);
    }

    private static CashMovementService
        CreateService(
            TreasuryDbContext context,
            AsyncTestBarrier barrier,
            Guid currentUserId)
    {
        var accountRepository =
            new CoordinatedAccountRepository(
                new AccountRepository(context),
                barrier);

        var ledgerRepository =
            new LedgerRepository(context);

        var transactionRepository =
            new TreasuryTransactionRepository(
                context);

        var paymentRequestRepository =
            new PaymentRequestRepository(
                context);

        var currentUserService =
            new Mock<ICurrentUserService>();

        currentUserService
            .SetupGet(service =>
                service.UserId)
            .Returns(currentUserId);
        
        var approvalPolicyService =
            new Mock<IApprovalPolicyService>();

        approvalPolicyService
            .Setup(service =>
                service.GetThreshold(
                    It.IsAny<string>(),
                    It.IsAny<string>()))
            .ReturnsAsync(10_000_000m);

        return new CashMovementService(
            accountRepository,
            ledgerRepository,
            transactionRepository,
            currentUserService.Object,
            paymentRequestRepository,
            approvalPolicyService.Object);
    }

    private static CreateCashPaymentDto
        CreatePayment(
            Guid accountId,
            string idempotencyKey)
    {
        return new CreateCashPaymentDto
        {
            AccountId =
                accountId,

            Amount =
                7_000_000m,

            BeneficiaryName =
                "Concurrent Supplier",

            Category =
                "SupplierPayment",

            ExternalReference =
                idempotencyKey,

            IdempotencyKey =
                idempotencyKey,

            Description =
                "Concurrent payment test"
        };
    }

    private static async Task<PaymentAttempt>
        CaptureAttempt(
            Func<Task<CashPaymentResponseDto>>
                action)
    {
        try
        {
            var response =
                await action();

            return new PaymentAttempt(
                true,
                response,
                null);
        }
        catch (Exception exception)
        {
            return new PaymentAttempt(
                false,
                null,
                exception);
        }
    }

    private static async Task<SeededData>
        SeedRequiredData(
            TreasuryDbContext context)
    {
        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name = Roles.TreasuryOfficer
        };

        var user = new User
        {
            Id = Guid.NewGuid(),

            FirstName =
                "Concurrent",

            LastName =
                "Tester",

            Email =
                $"concurrent-{Guid.NewGuid():N}" +
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

        var accountType =
            new AccountType
            {
                Id = Guid.NewGuid(),
                Name = AccountTypes.Operating
            };

        var account = new Account
        {
            Id = Guid.NewGuid(),

            Name =
                "Concurrency Test Account",

            AccountNumber =
                $"CONCURRENT-{Guid.NewGuid():N}",

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

        await context.Roles.AddAsync(role);
        await context.Users.AddAsync(user);
        await context.AccountTypes
            .AddAsync(accountType);
        await context.Accounts.AddAsync(account);

        await context.SaveChangesAsync();

        return new SeededData(
            user.Id,
            account.Id);
    }

    private sealed record SeededData(
        Guid UserId,
        Guid AccountId);

    private sealed record PaymentAttempt(
        bool Succeeded,
        CashPaymentResponseDto? Response,
        Exception? Exception);
}
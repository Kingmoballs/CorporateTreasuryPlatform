using Microsoft.EntityFrameworkCore;
using Moq;
using Treasury.Application.DTOs.CashMovements;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Repositories;
using Treasury.Infrastructure.Services;
using Treasury.Shared.Constants;
using Treasury.Infrastructure.Persistence;
using Treasury.Application.DTOs.ApprovalPolicies;

namespace Treasury.Tests.Integration;

public class CashReceiptIntegrationTests
{
    [Fact]
    public async Task RecordReceipt_PersistsBalanceTransactionAndLedgerOnce()
    {
        // Arrange
        await using var database =
            await PostgreSqlTestDatabase.Start();

        await using var context =
            database.CreateContext();

        var seededData =
            await SeedRequiredData(context);

        var accountRepository =
            new AccountRepository(context);

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
            .Returns(seededData.UserId);
        
        var approvalPolicyService =
            new Mock<IApprovalPolicyService>();
        
        var approvalDecisionRepository =
            new Mock<IApprovalDecisionRepository>();

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

        var service =
            new CashMovementService(
                accountRepository,
                ledgerRepository,
                transactionRepository,
                currentUserService.Object,
                paymentRequestRepository,
                approvalPolicyService.Object,
                new ApprovalDecisionRepository(context),
                new AuditLogService(
                    new AuditLogRepository(context),
                    currentUserService.Object));

        var dto = new CreateCashReceiptDto
        {
            AccountId =
                seededData.AccountId,

            Amount =
                2_500_000m,

            CounterpartyName =
                "Acme Distribution Limited",

            Category =
                "CustomerReceipt",

            ExternalReference =
                "BANK-CR-TEST-001",

            IdempotencyKey =
                "integration-receipt-001",

            Description =
                "Integration-test receipt"
        };

        // Act
        var firstResponse =
            await service.RecordReceipt(dto);

        /*
         * Retrying with the same idempotency key
         * must return the existing transaction.
         */
        var retryResponse =
            await service.RecordReceipt(dto);

        context.ChangeTracker.Clear();

        var account =
            await context.Accounts
                .AsNoTracking()
                .SingleAsync(item =>
                    item.Id ==
                        seededData.AccountId);

        var transactions =
            await context
                .TreasuryTransactions
                .AsNoTracking()
                .Where(transaction =>
                    transaction.IdempotencyKey ==
                        dto.IdempotencyKey)
                .ToListAsync();

        var ledgerEntries =
            await context.LedgerEntries
                .AsNoTracking()
                .Where(entry =>
                    entry.TreasuryTransactionId ==
                        firstResponse.TransactionId)
                .ToListAsync();

        // Assert
        Assert.Equal(
            12_500_000m,
            account.Balance);

        Assert.Equal(
            firstResponse.TransactionId,
            retryResponse.TransactionId);

        Assert.Equal(
            firstResponse.TransactionReference,
            retryResponse.TransactionReference);

        var transaction =
            Assert.Single(transactions);

        Assert.Equal(
            TransactionTypes.CashReceipt,
            transaction.TransactionType);

        Assert.Equal(
            2_500_000m,
            transaction.Amount);

        var ledgerEntry =
            Assert.Single(ledgerEntries);

        Assert.Equal(
            "Debit",
            ledgerEntry.EntryType);

        Assert.Equal(
            2_500_000m,
            ledgerEntry.Amount);

        Assert.Equal(
            transaction.Id,
            ledgerEntry.TreasuryTransactionId);
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

            FirstName = "Integration",

            LastName = "Tester",

            Email =
                $"integration-{Guid.NewGuid():N}" +
                "@example.com",

            PasswordHash =
                "not-used-by-this-test",

            RoleId = role.Id,

            Role = role,

            IsActive = true,

            CreatedAt = DateTime.UtcNow
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
                "Integration Test Account",

            AccountNumber =
                $"TEST-{Guid.NewGuid():N}",

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
}
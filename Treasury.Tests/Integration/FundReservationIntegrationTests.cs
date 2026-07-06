using Microsoft.EntityFrameworkCore;
using Moq;
using Treasury.Application.Common.Exceptions;
using Treasury.Application.DTOs.CashMovements;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;
using Treasury.Infrastructure.Repositories;
using Treasury.Infrastructure.Services;
using Treasury.Shared.Constants;
using Treasury.Application.DTOs.ApprovalPolicies;

namespace Treasury.Tests.Integration;

public class FundReservationIntegrationTests
{
    [Fact]
    public async Task LargePayment_ReservesFunds_AndRejectionReleasesThem()
    {
        // Arrange
        await using var database =
            await PostgreSqlTestDatabase.Start();

        var seeded =
            await SeedRequiredData(database);

        Guid paymentRequestId;

        // Act: requester submits a ₦12M payment.
        await using (
            var requestContext =
                database.CreateContext())
        {
            var service =
                CreateCashMovementService(
                    requestContext,
                    seeded.RequesterId);

            var response =
                await service.RecordPayment(
                    CreatePayment(
                        seeded.AccountId,
                        12_000_000m,
                        "reservation-rejection-001"));

            Assert.Equal(
                ApprovalStatus.Pending,
                response.Status);

            Assert.NotNull(
                response.PaymentRequestId);

            paymentRequestId =
                response.PaymentRequestId!.Value;
        }

        // Assert: balance is unchanged, but funds are reserved.
        await using (
            var reservationContext =
                database.CreateContext())
        {
            var account =
                await reservationContext.Accounts
                    .AsNoTracking()
                    .SingleAsync(item =>
                        item.Id ==
                            seeded.AccountId);

            Assert.Equal(
                20_000_000m,
                account.Balance);

            Assert.Equal(
                12_000_000m,
                account.ReservedBalance);

            Assert.Equal(
                8_000_000m,
                account.AvailableBalance);
        }

        // Act: a different user rejects the request.
        await using (
            var rejectionContext =
                database.CreateContext())
        {
            var service =
                CreateCashMovementService(
                    rejectionContext,
                    seeded.ApproverId);

            var response =
                await service.RejectPayment(
                    paymentRequestId,
                    "Payment documentation is incomplete.");

            Assert.Equal(
                ApprovalStatus.Rejected,
                response.Status);
        }

        // Assert: rejection releases the reservation.
        await using var verificationContext =
            database.CreateContext();

        var accountAfterRejection =
            await verificationContext.Accounts
                .AsNoTracking()
                .SingleAsync(item =>
                    item.Id ==
                        seeded.AccountId);

        var requestAfterRejection =
            await verificationContext
                .PaymentRequests
                .AsNoTracking()
                .SingleAsync(request =>
                    request.Id ==
                        paymentRequestId);

        Assert.Equal(
            20_000_000m,
            accountAfterRejection.Balance);

        Assert.Equal(
            0m,
            accountAfterRejection
                .ReservedBalance);

        Assert.Equal(
            20_000_000m,
            accountAfterRejection
                .AvailableBalance);

        Assert.Equal(
            ApprovalStatus.Rejected,
            requestAfterRejection.Status);

        Assert.Equal(
            seeded.ApproverId,
            requestAfterRejection
                .ReviewedByUserId);
    }

    [Fact]
    public async Task LargePayment_ApprovalConsumesReservationAndBalance()
    {
        // Arrange
        await using var database =
            await PostgreSqlTestDatabase.Start();

        var seeded =
            await SeedRequiredData(database);

        Guid paymentRequestId;

        await using (
            var requestContext =
                database.CreateContext())
        {
            var requesterService =
                CreateCashMovementService(
                    requestContext,
                    seeded.RequesterId);

            var response =
                await requesterService
                    .RecordPayment(
                        CreatePayment(
                            seeded.AccountId,
                            12_000_000m,
                            "reservation-approval-001"));

            Assert.NotNull(
                response.PaymentRequestId);

            paymentRequestId =
                response.PaymentRequestId!.Value;
        }

        // Act: a different user approves the payment.
        Guid transactionId;

        await using (
            var approvalContext =
                database.CreateContext())
        {
            var approverService =
                CreateCashMovementService(
                    approvalContext,
                    seeded.ApproverId);

            var response =
                await approverService
                    .ApprovePayment(
                        paymentRequestId);

            Assert.Equal(
                TransactionStatuses.Completed,
                response.Status);

            Assert.NotNull(
                response.TransactionId);

            transactionId =
                response.TransactionId!.Value;
        }

        // Assert
        await using var verificationContext =
            database.CreateContext();

        var account =
            await verificationContext.Accounts
                .AsNoTracking()
                .SingleAsync(item =>
                    item.Id ==
                        seeded.AccountId);

        var paymentRequest =
            await verificationContext
                .PaymentRequests
                .AsNoTracking()
                .SingleAsync(request =>
                    request.Id ==
                        paymentRequestId);

        var transaction =
            await verificationContext
                .TreasuryTransactions
                .AsNoTracking()
                .SingleAsync(item =>
                    item.Id ==
                        transactionId);

        var ledgerEntry =
            await verificationContext
                .LedgerEntries
                .AsNoTracking()
                .SingleAsync(entry =>
                    entry.TreasuryTransactionId ==
                        transactionId);

        Assert.Equal(
            8_000_000m,
            account.Balance);

        Assert.Equal(
            0m,
            account.ReservedBalance);

        Assert.Equal(
            8_000_000m,
            account.AvailableBalance);

        Assert.Equal(
            ApprovalStatus.Approved,
            paymentRequest.Status);

        Assert.Equal(
            TransactionTypes.CashPayment,
            transaction.TransactionType);

        Assert.Equal(
            12_000_000m,
            transaction.Amount);

        Assert.Equal(
            "Credit",
            ledgerEntry.EntryType);

        Assert.Equal(
            12_000_000m,
            ledgerEntry.Amount);
    }

    [Fact]
    public async Task SecondPendingPayment_CannotOverReserveAccount()
    {
        // Arrange
        await using var database =
            await PostgreSqlTestDatabase.Start();

        var seeded =
            await SeedRequiredData(database);

        // First request reserves ₦12M from ₦20M.
        await using (
            var firstContext =
                database.CreateContext())
        {
            var service =
                CreateCashMovementService(
                    firstContext,
                    seeded.RequesterId);

            var response =
                await service.RecordPayment(
                    CreatePayment(
                        seeded.AccountId,
                        12_000_000m,
                        "over-reservation-001"));

            Assert.Equal(
                ApprovalStatus.Pending,
                response.Status);
        }

        // Act: another ₦11M request exceeds ₦8M available.
        await using (
            var secondContext =
                database.CreateContext())
        {
            var service =
                CreateCashMovementService(
                    secondContext,
                    seeded.RequesterId);

            var exception =
                await Assert.ThrowsAsync<
                    BusinessRuleException>(
                        () =>
                            service.RecordPayment(
                                CreatePayment(
                                    seeded.AccountId,
                                    11_000_000m,
                                    "over-reservation-002")));

            Assert.Contains(
                "available funds",
                exception.Message);
        }

        // Assert: only the first request and reservation exist.
        await using var verificationContext =
            database.CreateContext();

        var account =
            await verificationContext.Accounts
                .AsNoTracking()
                .SingleAsync(item =>
                    item.Id ==
                        seeded.AccountId);

        var pendingRequests =
            await verificationContext
                .PaymentRequests
                .AsNoTracking()
                .Where(request =>
                    request.Status ==
                        ApprovalStatus.Pending)
                .ToListAsync();

        Assert.Equal(
            20_000_000m,
            account.Balance);

        Assert.Equal(
            12_000_000m,
            account.ReservedBalance);

        Assert.Equal(
            8_000_000m,
            account.AvailableBalance);

        Assert.Single(
            pendingRequests);
    }

    [Fact]
    public async Task
        LargePayment_TwoApprovals_ExecutesOnlyAfterFinalApproval()
    {
        // Arrange
        await using var database =
            await PostgreSqlTestDatabase.Start();

        var seeded =
            await SeedRequiredData(database);

        const decimal paymentAmount =
            12_000_000m;

        Guid paymentRequestId;

        // The policy requires two different approvers.
        await using (
            var requestContext =
                database.CreateContext())
        {
            var service =
                CreateCashMovementService(
                    requestContext,
                    seeded.RequesterId,
                    requiredApprovalCount: 2);

            var response =
                await service.RecordPayment(
                    CreatePayment(
                        seeded.AccountId,
                        paymentAmount,
                        "multi-level-payment-001"));

            Assert.Equal(
                ApprovalStatus.Pending,
                response.Status);

            Assert.Equal(
                0,
                response.ApprovalCount);

            Assert.Equal(
                2,
                response.RequiredApprovalCount);

            Assert.NotNull(
                response.PaymentRequestId);

            paymentRequestId =
                response.PaymentRequestId!.Value;
        }

        // First approval must not execute the payment.
        await using (
            var firstApprovalContext =
                database.CreateContext())
        {
            var service =
                CreateCashMovementService(
                    firstApprovalContext,
                    seeded.ApproverId,
                    requiredApprovalCount: 2);

            var response =
                await service.ApprovePayment(
                    paymentRequestId);

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

        // Confirm that funds remain reserved after approval one.
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
                await intermediateContext.PaymentRequests
                    .AsNoTracking()
                    .SingleAsync(item =>
                        item.Id == paymentRequestId);

            var transactionCount =
                await intermediateContext
                    .TreasuryTransactions
                    .CountAsync(item =>
                        item.PaymentRequestId ==
                            paymentRequestId);

            var decisions =
                await intermediateContext
                    .ApprovalDecisions
                    .AsNoTracking()
                    .Where(item =>
                        item.PaymentRequestId ==
                            paymentRequestId)
                    .ToListAsync();

            Assert.Equal(
                20_000_000m,
                account.Balance);

            Assert.Equal(
                paymentAmount,
                account.ReservedBalance);

            Assert.Equal(
                ApprovalStatus.Pending,
                request.Status);

            Assert.Equal(
                1,
                request.ApprovalCount);

            Assert.Equal(
                0,
                transactionCount);

            Assert.Single(decisions);
        }

        // Second distinct approver completes the payment.
        await using (
            var finalApprovalContext =
                database.CreateContext())
        {
            var service =
                CreateCashMovementService(
                    finalApprovalContext,
                    seeded.SecondApproverId,
                    requiredApprovalCount: 2);

            var response =
                await service.ApprovePayment(
                    paymentRequestId);

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

        var finalAccount =
            await verificationContext.Accounts
                .AsNoTracking()
                .SingleAsync(item =>
                    item.Id == seeded.AccountId);

        var finalRequest =
            await verificationContext.PaymentRequests
                .AsNoTracking()
                .SingleAsync(item =>
                    item.Id == paymentRequestId);

        var transactions =
            await verificationContext
                .TreasuryTransactions
                .AsNoTracking()
                .Where(item =>
                    item.PaymentRequestId ==
                        paymentRequestId)
                .ToListAsync();

        var finalDecisions =
            await verificationContext
                .ApprovalDecisions
                .AsNoTracking()
                .Where(item =>
                    item.PaymentRequestId ==
                        paymentRequestId)
                .ToListAsync();

        Assert.Equal(
            8_000_000m,
            finalAccount.Balance);

        Assert.Equal(
            0m,
            finalAccount.ReservedBalance);

        Assert.Equal(
            ApprovalStatus.Approved,
            finalRequest.Status);

        Assert.Equal(
            2,
            finalRequest.ApprovalCount);

        Assert.Equal(
            seeded.SecondApproverId,
            finalRequest.ReviewedByUserId);

        Assert.Single(transactions);

        Assert.Equal(
            2,
            finalDecisions.Count);

        Assert.Contains(
            finalDecisions,
            item =>
                item.ApproverUserId ==
                    seeded.ApproverId);

        Assert.Contains(
            finalDecisions,
            item =>
                item.ApproverUserId ==
                    seeded.SecondApproverId);
    }

    private static CashMovementService
        CreateCashMovementService(
            TreasuryDbContext context,
            Guid userId,
            int requiredApprovalCount = 1)
    {
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
                        requiredApprovalCount
                });


        /*
         * If your constructor parameter order differs,
         * adjust only this constructor call.
         */
        return new CashMovementService(
            accountRepository,
            ledgerRepository,
            transactionRepository,
            currentUser.Object,
            paymentRequestRepository,
            approvalPolicyService.Object,
            new ApprovalDecisionRepository(context));
    }

    private static CreateCashPaymentDto
        CreatePayment(
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
                "Reservation Test Supplier",

            Category =
                "SupplierPayment",

            ExternalReference =
                idempotencyKey,

            IdempotencyKey =
                idempotencyKey,

            Description =
                "Fund reservation integration test"
        };
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
                "reservation-requester");

        var approver =
            CreateUser(
                managerRole,
                "reservation-approver");
        
        var secondApprover =
            CreateUser(
                managerRole,
                "reservation-second-approver");

        var accountType = new AccountType
        {
            Id = Guid.NewGuid(),
            Name = AccountTypes.Operating
        };

        var account = new Account
        {
            Id = Guid.NewGuid(),

            Name =
                "Fund Reservation Test Account",

            AccountNumber =
                $"RESERVE-{Guid.NewGuid():N}",

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
        Guid ApproverId,
        Guid SecondApproverId,
        Guid AccountId);
}
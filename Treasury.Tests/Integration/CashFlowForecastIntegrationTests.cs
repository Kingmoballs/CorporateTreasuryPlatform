using Microsoft.EntityFrameworkCore;
using Moq;
using Treasury.Application.DTOs.CashFlowForecasts;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;
using Treasury.Infrastructure.Repositories;
using Treasury.Infrastructure.Services;
using Treasury.Shared.Constants;

namespace Treasury.Tests.Integration;

public class CashFlowForecastIntegrationTests
{
    [Fact]
    public async Task ForecastReport_UsesActiveItemsAndDetectsLiquidityGaps()
    {
        // Arrange
        await using var database =
            await PostgreSqlTestDatabase.Start();

        var seeded =
            await SeedRequiredData(
                database,
                openingBalance: 5_000_000m);

        var today =
            DateTime.UtcNow.Date;

        await using var context =
            database.CreateContext();

        var service =
            CreateForecastService(
                context,
                seeded.UserId);

        await service.Create(
            new CreateCashFlowForecastItemDto
            {
                AccountId =
                    seeded.AccountId,

                Direction =
                    CashFlowDirections.Outflow,

                Amount =
                    8_000_000m,

                Currency =
                    "NGN",

                ExpectedDateUtc =
                    today.AddDays(1).AddHours(9),

                Category =
                    "SupplierPayment",

                CounterpartyName =
                    "XYZ Services",

                Description =
                    "Expected supplier payment",

                SourceType =
                    CashFlowForecastSourceTypes.SupplierPayment
            });

        await service.Create(
            new CreateCashFlowForecastItemDto
            {
                AccountId =
                    seeded.AccountId,

                Direction =
                    CashFlowDirections.Inflow,

                Amount =
                    2_000_000m,

                Currency =
                    "NGN",

                ExpectedDateUtc =
                    today.AddDays(2).AddHours(10),

                Category =
                    "CustomerReceipt",

                CounterpartyName =
                    "ABC Limited",

                Description =
                    "Expected customer receipt",

                SourceType =
                    CashFlowForecastSourceTypes.CustomerReceipt
            });

        // Act
        var report =
            await service.GetForecastReport(
                seeded.AccountId,
                currency: null,
                fromUtc: today,
                toUtc: today.AddDays(2),
                minimumLiquidityThreshold: 1_000_000m);

        // Assert
        Assert.Equal(
            5_000_000m,
            report.OpeningAvailableBalance);

        Assert.Equal(
            2_000_000m,
            report.TotalExpectedInflow);

        Assert.Equal(
            8_000_000m,
            report.TotalExpectedOutflow);

        Assert.Equal(
            -6_000_000m,
            report.NetMovement);

        Assert.Equal(
            -1_000_000m,
            report.ProjectedClosingBalance);

        Assert.Equal(
            -3_000_000m,
            report.MinimumProjectedBalance);

        Assert.Equal(
            2,
            report.LiquidityGapDayCount);

        var outflowDay =
            Assert.Single(
                report.DailyForecasts,
                day =>
                    day.DateUtc ==
                    today.AddDays(1));

        Assert.Equal(
            8_000_000m,
            outflowDay.ExpectedOutflow);

        Assert.True(
            outflowDay.IsLiquidityGap);

        Assert.Equal(
            4_000_000m,
            outflowDay.LiquidityGapAmount);

        var inflowDay =
            Assert.Single(
                report.DailyForecasts,
                day =>
                    day.DateUtc ==
                    today.AddDays(2));

        Assert.Equal(
            2_000_000m,
            inflowDay.ExpectedInflow);

        Assert.True(
            inflowDay.IsLiquidityGap);
    }

    [Fact]
    public async Task CancelForecastItem_RemovesItemFromActiveForecasts()
    {
        // Arrange
        await using var database =
            await PostgreSqlTestDatabase.Start();

        var seeded =
            await SeedRequiredData(
                database,
                openingBalance: 20_000_000m);

        var today =
            DateTime.UtcNow.Date;

        await using var context =
            database.CreateContext();

        var service =
            CreateForecastService(
                context,
                seeded.UserId);

        var item =
            await service.Create(
                new CreateCashFlowForecastItemDto
                {
                    AccountId =
                        seeded.AccountId,

                    Direction =
                        CashFlowDirections.Outflow,

                    Amount =
                        4_000_000m,

                    Currency =
                        "NGN",

                    ExpectedDateUtc =
                        today.AddDays(1).AddHours(12),

                    Category =
                        "Tax",

                    CounterpartyName =
                        "Tax Authority",

                    Description =
                        "Expected tax payment",

                    SourceType =
                        CashFlowForecastSourceTypes.Tax
                });

        // Act
        var cancelled =
            await service.Cancel(
                item.Id);

        var activeItems =
            await service.GetActive(
                seeded.AccountId,
                currency: null,
                fromUtc: today,
                toUtc: today.AddDays(3));

        var report =
            await service.GetForecastReport(
                seeded.AccountId,
                currency: null,
                fromUtc: today,
                toUtc: today.AddDays(3),
                minimumLiquidityThreshold: 0m);

        // Assert
        Assert.Equal(
            CashFlowForecastStatus.Cancelled,
            cancelled.Status);

        Assert.Equal(
            seeded.UserId,
            cancelled.CancelledByUserId);

        Assert.DoesNotContain(
            activeItems,
            activeItem =>
                activeItem.Id == item.Id);

        Assert.Equal(
            0m,
            report.TotalExpectedOutflow);

        Assert.Equal(
            20_000_000m,
            report.ProjectedClosingBalance);
    }

    [Fact]
    public async Task RealizeForecastItem_LinksTransactionAndRemovesFromActiveForecasts()
    {
        // Arrange
        await using var database =
            await PostgreSqlTestDatabase.Start();

        var seeded =
            await SeedRequiredData(
                database,
                openingBalance: 10_000_000m);

        var today =
            DateTime.UtcNow.Date;

        Guid transactionId;

        await using var context =
            database.CreateContext();

        var service =
            CreateForecastService(
                context,
                seeded.UserId);

        var forecastItem =
            await service.Create(
                new CreateCashFlowForecastItemDto
                {
                    AccountId =
                        seeded.AccountId,

                    Direction =
                        CashFlowDirections.Inflow,

                    Amount =
                        2_500_000m,

                    Currency =
                        "NGN",

                    ExpectedDateUtc =
                        today.AddDays(1).AddHours(10),

                    Category =
                        "CustomerReceipt",

                    CounterpartyName =
                        "ABC Limited",

                    Description =
                        "Expected customer receipt",

                    SourceType =
                        CashFlowForecastSourceTypes.CustomerReceipt
                });

        var transaction =
            CreateCompletedTransaction(
                seeded.AccountId,
                amount: 2_500_000m,
                signedAmountIsInflow: true,
                referencePrefix: "FORECAST-REALIZE");

        await context.TreasuryTransactions
            .AddAsync(transaction);

        await context.SaveChangesAsync();

        transactionId =
            transaction.Id;

        // Act
        var realized =
            await service.Realize(
                forecastItem.Id,
                transactionId);

        var activeItems =
            await service.GetActive(
                seeded.AccountId,
                currency: null,
                fromUtc: today,
                toUtc: today.AddDays(3));

        var report =
            await service.GetForecastReport(
                seeded.AccountId,
                currency: null,
                fromUtc: today,
                toUtc: today.AddDays(3),
                minimumLiquidityThreshold: 0m);

        // Assert
        Assert.Equal(
            CashFlowForecastStatus.Realized,
            realized.Status);

        Assert.Equal(
            transactionId,
            realized.RealizedTreasuryTransactionId);

        Assert.NotNull(
            realized.RealizedAtUtc);

        Assert.DoesNotContain(
            activeItems,
            activeItem =>
                activeItem.Id == forecastItem.Id);

        Assert.Equal(
            0m,
            report.TotalExpectedInflow);

        Assert.Equal(
            10_000_000m,
            report.ProjectedClosingBalance);
    }

    private static CashFlowForecastService CreateForecastService(
        TreasuryDbContext context,
        Guid userId)
    {
        return new CashFlowForecastService(
            new CashFlowForecastRepository(context),
            new AccountRepository(context),
            CreateCurrentUser(userId),
            new TreasuryTransactionRepository(context));
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

        currentUser
            .SetupGet(service =>
                service.Email)
            .Returns("forecast-test@example.com");

        currentUser
            .SetupGet(service =>
                service.Role)
            .Returns(Roles.TreasuryOfficer);

        return currentUser.Object;
    }

    private static TreasuryTransaction CreateCompletedTransaction(
        Guid accountId,
        decimal amount,
        bool signedAmountIsInflow,
        string referencePrefix)
    {
        return new TreasuryTransaction
        {
            Id =
                Guid.NewGuid(),

            Reference =
                $"{referencePrefix}-{Guid.NewGuid():N}",

            TransactionType =
                signedAmountIsInflow
                    ? TransactionTypes.CashReceipt
                    : TransactionTypes.CashPayment,

            Status =
                TransactionStatuses.Completed,

            Amount =
                amount,

            Currency =
                "NGN",

            Description =
                signedAmountIsInflow
                    ? "Receipt for forecast realization test"
                    : "Payment for forecast realization test",

            SourceAccountId =
                signedAmountIsInflow
                    ? null
                    : accountId,

            DestinationAccountId =
                signedAmountIsInflow
                    ? accountId
                    : null,

            Category =
                signedAmountIsInflow
                    ? "CustomerReceipt"
                    : "SupplierPayment",

            CounterpartyName =
                signedAmountIsInflow
                    ? "ABC Limited"
                    : "XYZ Services",

            ExternalReference =
                $"{referencePrefix}-EXT-{Guid.NewGuid():N}",

            IdempotencyKey =
                $"{referencePrefix}-IDEMP-{Guid.NewGuid():N}",

            CreatedAtUtc =
                DateTime.UtcNow,

            CompletedAtUtc =
                DateTime.UtcNow
        };
    }

    private static async Task<SeededData> SeedRequiredData(
        PostgreSqlTestDatabase database,
        decimal openingBalance)
    {
        await using var context =
            database.CreateContext();

        var role =
            new Role
            {
                Id =
                    Guid.NewGuid(),

                Name =
                    Roles.TreasuryOfficer
            };

        var user =
            new User
            {
                Id =
                    Guid.NewGuid(),

                FirstName =
                    "Forecast",

                LastName =
                    "Tester",

                Email =
                    $"forecast-{Guid.NewGuid():N}@example.com",

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
                Id =
                    Guid.NewGuid(),

                Name =
                    AccountTypes.Operating
            };

        var account =
            new Account
            {
                Id =
                    Guid.NewGuid(),

                Name =
                    "Forecast Test Account",

                AccountNumber =
                    $"FORECAST-{Guid.NewGuid():N}",

                Balance =
                    openingBalance,

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
            role);

        await context.Users.AddAsync(
            user);

        await context.AccountTypes.AddAsync(
            accountType);

        await context.Accounts.AddAsync(
            account);

        await context.SaveChangesAsync();

        return new SeededData(
            user.Id,
            account.Id);
    }

    private sealed record SeededData(
        Guid UserId,
        Guid AccountId);
}
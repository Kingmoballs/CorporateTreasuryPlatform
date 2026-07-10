using Moq;
using Treasury.Application.DTOs.Fx;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;
using Treasury.Infrastructure.Repositories;
using Treasury.Infrastructure.Services;
using Treasury.Shared.Constants;

namespace Treasury.Tests.Integration;

public class FxRateIntegrationTests
{
    [Fact]
    public async Task ConvertAmount_UsesDirectAndInverseRates()
    {
        // Arrange
        await using var database =
            await PostgreSqlTestDatabase.Start();

        var seeded =
            await SeedRequiredData(database);

        await using var context =
            database.CreateContext();

        var service =
            CreateFxRateService(
                context,
                seeded.UserId);

        var fxRate =
            await service.Create(
                new CreateFxRateDto
                {
                    FromCurrency =
                        "USD",

                    ToCurrency =
                        "NGN",

                    Rate =
                        1500m,

                    RateDateUtc =
                        DateTime.UtcNow.Date,

                    SourceType =
                        FxRateSourceTypes.Manual,

                    SourceReference =
                        "Integration test rate",

                    IsActive =
                        true
                });

        // Act
        var directConversion =
            await service.ConvertAmount(
                amount: 1_000m,
                fromCurrency: "USD",
                toCurrency: "NGN",
                asOfUtc: DateTime.UtcNow);

        var inverseConversion =
            await service.ConvertAmount(
                amount: 1_500_000m,
                fromCurrency: "NGN",
                toCurrency: "USD",
                asOfUtc: DateTime.UtcNow);

        // Assert
        Assert.Equal(
            fxRate.Id,
            directConversion.FxRateId);

        Assert.False(
            directConversion.UsedInverseRate);

        Assert.Equal(
            1_500_000m,
            directConversion.ConvertedAmount);

        Assert.Equal(
            fxRate.Id,
            inverseConversion.FxRateId);

        Assert.True(
            inverseConversion.UsedInverseRate);

        Assert.Equal(
            1_000m,
            inverseConversion.ConvertedAmount);
    }

    [Fact]
    public async Task ConsolidatedCashPosition_ConvertsAccountsToBaseCurrency()
    {
        // Arrange
        await using var database =
            await PostgreSqlTestDatabase.Start();

        var seeded =
            await SeedRequiredData(database);

        await using var context =
            database.CreateContext();

        var service =
            CreateFxRateService(
                context,
                seeded.UserId);

        await service.Create(
            new CreateFxRateDto
            {
                FromCurrency =
                    "USD",

                ToCurrency =
                    "NGN",

                Rate =
                    1500m,

                RateDateUtc =
                    DateTime.UtcNow.Date,

                SourceType =
                    FxRateSourceTypes.Manual,

                SourceReference =
                    "Integration test rate",

                IsActive =
                    true
            });

        // Act
        var report =
            await service.GetConsolidatedCashPosition(
                baseCurrency: "NGN",
                asOfUtc: DateTime.UtcNow);

        // Assert
        Assert.Equal(
            "NGN",
            report.BaseCurrency);

        Assert.Equal(
            2,
            report.AccountCount);

        Assert.Equal(
            13_000_000m,
            report.TotalBalanceInBaseCurrency);

        Assert.Equal(
            11_850_000m,
            report.TotalAvailableBalanceInBaseCurrency);

        Assert.Equal(
            1_150_000m,
            report.TotalReservedBalanceInBaseCurrency);

        var usdAccount =
            Assert.Single(
                report.Accounts,
                account =>
                    account.Currency == "USD");

        Assert.Equal(
            1500m,
            usdAccount.EffectiveRate);

        Assert.Equal(
            3_000_000m,
            usdAccount.ConvertedBalance);

        Assert.Equal(
            2_850_000m,
            usdAccount.ConvertedAvailableBalance);

        Assert.Equal(
            150_000m,
            usdAccount.ConvertedReservedBalance);
    }

    [Fact]
    public async Task CurrencyExposureReport_GroupsBalancesByCurrency()
    {
        // Arrange
        await using var database =
            await PostgreSqlTestDatabase.Start();

        var seeded =
            await SeedRequiredData(database);

        await using var context =
            database.CreateContext();

        var service =
            CreateFxRateService(
                context,
                seeded.UserId);

        await service.Create(
            new CreateFxRateDto
            {
                FromCurrency =
                    "USD",

                ToCurrency =
                    "NGN",

                Rate =
                    1500m,

                RateDateUtc =
                    DateTime.UtcNow.Date,

                SourceType =
                    FxRateSourceTypes.Manual,

                SourceReference =
                    "Integration test rate",

                IsActive =
                    true
            });

        // Act
        var report =
            await service.GetCurrencyExposureReport(
                baseCurrency: "NGN",
                asOfUtc: DateTime.UtcNow);

        // Assert
        Assert.Equal(
            "NGN",
            report.BaseCurrency);

        Assert.Equal(
            11_850_000m,
            report.TotalAvailableLiquidityInBaseCurrency);

        Assert.Equal(
            2,
            report.Exposures.Count);

        var ngnExposure =
            Assert.Single(
                report.Exposures,
                exposure =>
                    exposure.Currency == "NGN");

        var usdExposure =
            Assert.Single(
                report.Exposures,
                exposure =>
                    exposure.Currency == "USD");

        Assert.Equal(
            9_000_000m,
            ngnExposure.TotalAvailableBalanceInBaseCurrency);

        Assert.Equal(
            2_850_000m,
            usdExposure.TotalAvailableBalanceInBaseCurrency);

        Assert.Equal(
            75.95m,
            ngnExposure.PercentageOfTotalAvailableLiquidity);

        Assert.Equal(
            24.05m,
            usdExposure.PercentageOfTotalAvailableLiquidity);
    }

    private static FxRateService CreateFxRateService(
        TreasuryDbContext context,
        Guid userId)
    {
        return new FxRateService(
            new FxRateRepository(context),
            new AccountRepository(context),
            CreateCurrentUser(userId),
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

        currentUser
            .SetupGet(service =>
                service.Email)
            .Returns("fx-test@example.com");

        currentUser
            .SetupGet(service =>
                service.Role)
            .Returns(Roles.FinanceManager);

        return currentUser.Object;
    }

    private static async Task<SeededData> SeedRequiredData(
        PostgreSqlTestDatabase database)
    {
        await using var context =
            database.CreateContext();

        var role =
            new Role
            {
                Id =
                    Guid.NewGuid(),

                Name =
                    Roles.FinanceManager
            };

        var user =
            new User
            {
                Id =
                    Guid.NewGuid(),

                FirstName =
                    "FX",

                LastName =
                    "Tester",

                Email =
                    $"fx-{Guid.NewGuid():N}@example.com",

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

        var ngnAccount =
            new Account
            {
                Id =
                    Guid.NewGuid(),

                Name =
                    "NGN Operating Account",

                AccountNumber =
                    $"FX-NGN-{Guid.NewGuid():N}",

                Balance =
                    10_000_000m,

                ReservedBalance =
                    1_000_000m,

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

        var usdAccount =
            new Account
            {
                Id =
                    Guid.NewGuid(),

                Name =
                    "USD Operating Account",

                AccountNumber =
                    $"FX-USD-{Guid.NewGuid():N}",

                Balance =
                    2_000m,

                ReservedBalance =
                    100m,

                Currency =
                    "USD",

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

        await context.AccountTypes.AddAsync(
            accountType);

        await context.Accounts.AddRangeAsync(
            ngnAccount,
            usdAccount);

        await context.SaveChangesAsync();

        return new SeededData(
            user.Id,
            ngnAccount.Id,
            usdAccount.Id);
    }

    private sealed record SeededData(
        Guid UserId,
        Guid NgnAccountId,
        Guid UsdAccountId);
}
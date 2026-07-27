using Moq;
using Treasury.Application.DTOs.CashFlowForecasts;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Services;
using Treasury.Shared.Constants;

namespace Treasury.Tests.Forecasts;

public class CashFlowForecastDimensionTests
{
    [Fact]
    public async Task
        ForecastReport_ScopesOpeningBalanceAndItems()
    {
        var setup = CreateSetup();
        var fromUtc =
            new DateTime(
                2026,
                7,
                24,
                0,
                0,
                0,
                DateTimeKind.Utc);
        var toUtc = fromUtc.AddDays(1);

        setup.Forecasts
            .Setup(item =>
                item.GetActiveForPeriod(
                    null,
                    "NGN",
                    It.IsAny<DateTime>(),
                    It.IsAny<DateTime>()))
            .ReturnsAsync(
                new List<CashFlowForecastItem>
                {
                    CreateForecast(
                        setup.SelectedAccount,
                        fromUtc,
                        300m),
                    CreateForecast(
                        setup.OtherAccount,
                        fromUtc,
                        500m),
                    CreateForecast(
                        null,
                        fromUtc,
                        900m)
                });

        var report =
            await setup.Service.GetForecastReport(
                null,
                "NGN",
                fromUtc,
                toUtc,
                0m,
                setup.LegalEntityId,
                setup.BusinessUnitId);

        Assert.Equal(
            setup.LegalEntityId,
            report.LegalEntityId);
        Assert.Equal(
            setup.BusinessUnitId,
            report.BusinessUnitId);
        Assert.Equal(
            900m,
            report.OpeningAvailableBalance);
        Assert.Equal(
            300m,
            report.TotalExpectedInflow);
        Assert.Equal(
            1_200m,
            report.ProjectedClosingBalance);

        var item =
            Assert.Single(
                report.DailyForecasts
                    .SelectMany(day => day.Items));

        Assert.Equal(
            setup.SelectedAccount.Id,
            item.AccountId);
        Assert.Equal(
            setup.LegalEntityId,
            item.LegalEntityId);
        Assert.Equal(
            setup.BusinessUnitId,
            item.BusinessUnitId);
    }

    [Fact]
    public async Task
        VarianceReport_ScopesForecastsActualsAndExport()
    {
        var setup = CreateSetup();
        var fromUtc =
            new DateTime(
                2026,
                7,
                24,
                0,
                0,
                0,
                DateTimeKind.Utc);
        var toUtc = fromUtc.AddDays(1);

        setup.Forecasts
            .Setup(item =>
                item.GetForVarianceReport(
                    null,
                    "NGN",
                    It.IsAny<DateTime>(),
                    It.IsAny<DateTime>()))
            .ReturnsAsync(
                new List<CashFlowForecastItem>
                {
                    CreateForecast(
                        setup.SelectedAccount,
                        fromUtc,
                        300m),
                    CreateForecast(
                        setup.OtherAccount,
                        fromUtc,
                        500m)
                });

        setup.Transactions
            .Setup(item =>
                item.GetCompletedCashFlowTransactionsForVariance(
                    null,
                    "NGN",
                    It.IsAny<DateTime>(),
                    It.IsAny<DateTime>()))
            .ReturnsAsync(
                new List<TreasuryTransaction>
                {
                    CreateReceipt(
                        setup.SelectedAccount.Id,
                        250m),
                    CreateReceipt(
                        setup.OtherAccount.Id,
                        700m)
                });

        var query =
            new CashFlowForecastVarianceQueryDto
            {
                Currency = "NGN",
                FromUtc = fromUtc,
                ToUtc = toUtc,
                LegalEntityId =
                    setup.LegalEntityId,
                BusinessUnitId =
                    setup.BusinessUnitId
            };

        var report =
            await setup.Service
                .GetVarianceReport(query);

        Assert.Equal(
            setup.LegalEntityId,
            report.LegalEntityId);
        Assert.Equal(
            setup.BusinessUnitId,
            report.BusinessUnitId);
        Assert.Equal(1, report.ForecastItemCount);
        Assert.Equal(1, report.ActualTransactionCount);
        Assert.Equal(
            300m,
            report.TotalForecastedInflow);
        Assert.Equal(
            250m,
            report.TotalActualInflow);
        Assert.Equal(-50m, report.NetVariance);

        var export =
            await setup.Service
                .ExportVarianceReportCsv(query);
        var csv =
            System.Text.Encoding.UTF8
                .GetString(export.Content);

        Assert.Contains(
            "LegalEntityId,BusinessUnitId",
            csv);
        Assert.Contains(
            setup.LegalEntityId.ToString(),
            csv);
        Assert.Contains(
            setup.BusinessUnitId.ToString(),
            csv);
    }

    private static TestSetup CreateSetup()
    {
        var organizationId = Guid.NewGuid();
        var legalEntityId = Guid.NewGuid();
        var businessUnitId = Guid.NewGuid();
        var selectedAccount =
            CreateAccount(
                organizationId,
                legalEntityId,
                businessUnitId,
                "SELECTED",
                1_000m,
                100m);
        var otherAccount =
            CreateAccount(
                organizationId,
                legalEntityId,
                Guid.NewGuid(),
                "OTHER",
                2_000m,
                0m);
        var forecasts =
            new Mock<ICashFlowForecastRepository>();
        var accounts =
            new Mock<IAccountRepository>();
        var transactions =
            new Mock<
                ITreasuryTransactionRepository>();

        accounts
            .Setup(item => item.GetAll())
            .ReturnsAsync(
                new List<Account>
                {
                    selectedAccount,
                    otherAccount
                });

        var service =
            new CashFlowForecastService(
                forecasts.Object,
                accounts.Object,
                new Mock<ICurrentUserService>()
                    .Object,
                transactions.Object,
                new Mock<IAuditLogService>()
                    .Object);

        return new TestSetup(
            service,
            forecasts,
            transactions,
            selectedAccount,
            otherAccount,
            legalEntityId,
            businessUnitId);
    }

    private static Account CreateAccount(
        Guid organizationId,
        Guid legalEntityId,
        Guid businessUnitId,
        string accountNumber,
        decimal balance,
        decimal reservedBalance)
    {
        return new Account
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            LegalEntityId = legalEntityId,
            BusinessUnitId = businessUnitId,
            Name = accountNumber,
            AccountNumber = accountNumber,
            Currency = "NGN",
            Balance = balance,
            ReservedBalance = reservedBalance,
            IsActive = true
        };
    }

    private static CashFlowForecastItem
        CreateForecast(
            Account? account,
            DateTime expectedDateUtc,
            decimal amount)
    {
        return new CashFlowForecastItem
        {
            Id = Guid.NewGuid(),
            AccountId = account?.Id,
            Account = account,
            Direction = CashFlowDirections.Inflow,
            Amount = amount,
            Currency = "NGN",
            ExpectedDateUtc = expectedDateUtc,
            Category = "Customer Receipt",
            Description = "Expected receipt",
            SourceType =
                CashFlowForecastSourceTypes
                    .CustomerReceipt,
            Status = CashFlowForecastStatus.Active
        };
    }

    private static TreasuryTransaction CreateReceipt(
        Guid accountId,
        decimal amount)
    {
        return new TreasuryTransaction
        {
            Id = Guid.NewGuid(),
            TransactionType =
                TransactionTypes.CashReceipt,
            Status = TransactionStatuses.Completed,
            Amount = amount,
            Currency = "NGN",
            DestinationAccountId = accountId,
            Category = "Customer Receipt"
        };
    }

    private sealed record TestSetup(
        CashFlowForecastService Service,
        Mock<ICashFlowForecastRepository> Forecasts,
        Mock<ITreasuryTransactionRepository>
            Transactions,
        Account SelectedAccount,
        Account OtherAccount,
        Guid LegalEntityId,
        Guid BusinessUnitId);
}

using Moq;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Services;
using Treasury.Shared.Constants;

namespace Treasury.Tests.Reporting;

public class TreasuryReportingServiceTests
{
    [Fact]
    public async Task GetBalanceAggregation_SeparatesBalancesByAccountType()
    {
        // Arrange
        var accountRepository =
            new Mock<IAccountRepository>();

        var transferRequestRepository =
            new Mock<ITransferRequestRepository>();

        var ledgerRepository =
            new Mock<ILedgerRepository>();

        var operatingType = new AccountType
        {
            Id = Guid.NewGuid(),
            Name = "Operating"
        };

        var payrollType = new AccountType
        {
            Id = Guid.NewGuid(),
            Name = "Payroll"
        };

        var reserveType = new AccountType
        {
            Id = Guid.NewGuid(),
            Name = "Reserve"
        };

        var accounts = new List<Account>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Operations Account",
                AccountNumber = "1000000001",
                Currency = "NGN",
                Balance = 45_000_000m,
                IsActive = true,
                AccountTypeId = operatingType.Id,
                AccountType = operatingType
            },

            new()
            {
                Id = Guid.NewGuid(),
                Name = "Payroll Account",
                AccountNumber = "1000000002",
                Currency = "ngn",
                Balance = 5_000_000m,
                IsActive = true,
                AccountTypeId = payrollType.Id,
                AccountType = payrollType
            },

            // Inactive accounts must not affect cash position.
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Closed Reserve Account",
                AccountNumber = "1000000003",
                Currency = "NGN",
                Balance = 100_000_000m,
                IsActive = false,
                AccountTypeId = reserveType.Id,
                AccountType = reserveType
            }
        };

        accountRepository
            .Setup(repository =>
                repository.GetAll())
            .ReturnsAsync(accounts);

        var service =
            new TreasuryReportingService(
                accountRepository.Object,
                transferRequestRepository.Object,
                ledgerRepository.Object);

        // Act
        var result =
            await service.GetBalanceAggregation();

        // Assert
        var currency =
            Assert.Single(result.Currencies);

        Assert.Equal("NGN", currency.Currency);
        Assert.Equal(2, currency.AccountCount);
        Assert.Equal(
            50_000_000m,
            currency.TotalBalance);

        Assert.Equal(
            2,
            currency.ByAccountType.Count);

        var operating =
            Assert.Single(
                currency.ByAccountType,
                item =>
                    item.AccountType ==
                        "Operating");

        Assert.Equal(
            45_000_000m,
            operating.TotalBalance);

        var payroll =
            Assert.Single(
                currency.ByAccountType,
                item =>
                    item.AccountType ==
                        "Payroll");

        Assert.Equal(
            5_000_000m,
            payroll.TotalBalance);
    }

    [Fact]
    public async Task
        GetBalanceAggregation_FiltersByOrganizationDimensions()
    {
        var legalEntityId = Guid.NewGuid();
        var businessUnitId = Guid.NewGuid();
        var accountType =
            new AccountType
            {
                Id = Guid.NewGuid(),
                Name = "Operating"
            };

        var accountRepository =
            new Mock<IAccountRepository>();

        accountRepository
            .Setup(repository =>
                repository.GetAll())
            .ReturnsAsync(
                new List<Account>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        LegalEntityId =
                            legalEntityId,
                        BusinessUnitId =
                            businessUnitId,
                        Name = "Selected",
                        AccountNumber = "SELECTED",
                        Currency = "NGN",
                        Balance = 250m,
                        IsActive = true,
                        AccountTypeId =
                            accountType.Id,
                        AccountType =
                            accountType
                    },
                    new()
                    {
                        Id = Guid.NewGuid(),
                        LegalEntityId =
                            legalEntityId,
                        BusinessUnitId =
                            Guid.NewGuid(),
                        Name = "Excluded",
                        AccountNumber = "EXCLUDED",
                        Currency = "NGN",
                        Balance = 750m,
                        IsActive = true,
                        AccountTypeId =
                            accountType.Id,
                        AccountType =
                            accountType
                    }
                });

        var service =
            new TreasuryReportingService(
                accountRepository.Object,
                new Mock<
                    ITransferRequestRepository>()
                    .Object,
                new Mock<ILedgerRepository>()
                    .Object);

        var result =
            await service.GetBalanceAggregation(
                legalEntityId,
                businessUnitId);

        var currency =
            Assert.Single(result.Currencies);

        Assert.Equal(
            legalEntityId,
            result.LegalEntityId);
        Assert.Equal(
            businessUnitId,
            result.BusinessUnitId);
        Assert.Equal(1, currency.AccountCount);
        Assert.Equal(250m, currency.TotalBalance);
    }

    [Fact]
    public async Task
        LiquidityReportAndExport_FilterBalancesAndActivity()
    {
        var legalEntityId = Guid.NewGuid();
        var businessUnitId = Guid.NewGuid();
        var accountType =
            new AccountType
            {
                Id = Guid.NewGuid(),
                Name = AccountTypes.Operating
            };
        var selectedAccount =
            new Account
            {
                Id = Guid.NewGuid(),
                LegalEntityId = legalEntityId,
                BusinessUnitId = businessUnitId,
                Name = "Selected",
                AccountNumber = "SELECTED",
                Currency = "NGN",
                Balance = 500m,
                ReservedBalance = 100m,
                IsActive = true,
                AccountTypeId = accountType.Id,
                AccountType = accountType
            };
        var otherAccount =
            new Account
            {
                Id = Guid.NewGuid(),
                LegalEntityId = legalEntityId,
                BusinessUnitId = Guid.NewGuid(),
                Name = "Excluded",
                AccountNumber = "EXCLUDED",
                Currency = "NGN",
                Balance = 900m,
                IsActive = true,
                AccountTypeId = accountType.Id,
                AccountType = accountType
            };

        var accountRepository =
            new Mock<IAccountRepository>();
        var ledgerRepository =
            new Mock<ILedgerRepository>();
        var transferRepository =
            new Mock<ITransferRequestRepository>();

        accountRepository
            .Setup(item => item.GetAll())
            .ReturnsAsync(
                new List<Account>
                {
                    selectedAccount,
                    otherAccount
                });
        ledgerRepository
            .Setup(item =>
                item.GetByDateRange(
                    It.IsAny<DateTime>(),
                    It.IsAny<DateTime>()))
            .ReturnsAsync(
                new List<LedgerEntry>
                {
                    CreateReceipt(
                        selectedAccount,
                        200m),
                    CreateReceipt(
                        otherAccount,
                        700m)
                });
        transferRepository
            .Setup(item => item.GetPending())
            .ReturnsAsync(
                new List<TransferRequest>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        FromAccountId =
                            selectedAccount.Id,
                        Amount = 50m
                    },
                    new()
                    {
                        Id = Guid.NewGuid(),
                        FromAccountId =
                            otherAccount.Id,
                        Amount = 80m
                    }
                });

        var service =
            new TreasuryReportingService(
                accountRepository.Object,
                transferRepository.Object,
                ledgerRepository.Object);
        var toUtc = DateTime.UtcNow;
        var fromUtc = toUtc.AddDays(-1);

        var report =
            await service.GetLiquidityReport(
                fromUtc,
                toUtc,
                legalEntityId,
                businessUnitId);

        var currency =
            Assert.Single(report.Currencies);

        Assert.Equal(
            legalEntityId,
            report.LegalEntityId);
        Assert.Equal(
            businessUnitId,
            report.BusinessUnitId);
        Assert.Equal(
            500m,
            currency.CurrentTotalCash);
        Assert.Equal(
            100m,
            currency.ReservedCash);
        Assert.Equal(
            400m,
            currency.AvailableLiquidity);
        Assert.Equal(
            1,
            currency.ExternalReceiptCount);
        Assert.Equal(
            200m,
            currency.ExternalReceiptAmount);
        Assert.Equal(
            1,
            currency.PendingInternalTransferCount);
        Assert.Equal(
            50m,
            currency.PendingInternalTransferAmount);

        var export =
            await service.ExportLiquidityReportCsv(
                fromUtc,
                toUtc,
                legalEntityId,
                businessUnitId);
        var csv =
            System.Text.Encoding.UTF8
                .GetString(export.Content);

        Assert.Contains(
            "LegalEntityId,BusinessUnitId",
            csv);
        Assert.Contains(
            legalEntityId.ToString(),
            csv);
        Assert.Contains(
            businessUnitId.ToString(),
            csv);
    }

    private static LedgerEntry CreateReceipt(
        Account account,
        decimal amount)
    {
        return new LedgerEntry
        {
            Id = Guid.NewGuid(),
            AccountId = account.Id,
            Account = account,
            Amount = amount,
            EntryType = "Debit",
            TreasuryTransaction =
                new TreasuryTransaction
                {
                    Id = Guid.NewGuid(),
                    TransactionType =
                        TransactionTypes
                            .CashReceipt
                }
        };
    }
}

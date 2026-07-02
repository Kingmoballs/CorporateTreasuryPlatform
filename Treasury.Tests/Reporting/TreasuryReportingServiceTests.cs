using Moq;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Services;

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
                currency.ByAccountType.Where(
                    item =>
                        item.AccountType ==
                            "Operating"));

        Assert.Equal(
            45_000_000m,
            operating.TotalBalance);

        var payroll =
            Assert.Single(
                currency.ByAccountType.Where(
                    item =>
                        item.AccountType ==
                            "Payroll"));

        Assert.Equal(
            5_000_000m,
            payroll.TotalBalance);
    }
}
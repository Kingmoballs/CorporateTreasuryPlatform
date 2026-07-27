using Moq;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Services;

namespace Treasury.Tests.Fx;

public class FxRateDimensionFilterTests
{
    [Fact]
    public async Task
        ConsolidatedCashPosition_AppliesOrganizationDimensions()
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
            .Setup(item => item.GetAll())
            .ReturnsAsync(
                new List<Account>
                {
                    CreateAccount(
                        accountType,
                        legalEntityId,
                        businessUnitId,
                        "SELECTED",
                        400m),
                    CreateAccount(
                        accountType,
                        legalEntityId,
                        Guid.NewGuid(),
                        "EXCLUDED",
                        600m)
                });

        var service =
            new FxRateService(
                new Mock<IFxRateRepository>()
                    .Object,
                accountRepository.Object,
                new Mock<ICurrentUserService>()
                    .Object,
                new Mock<IAuditLogService>()
                    .Object);

        var result =
            await service
                .GetConsolidatedCashPosition(
                    "NGN",
                    null,
                    legalEntityId,
                    businessUnitId);

        var account =
            Assert.Single(result.Accounts);

        Assert.Equal(
            legalEntityId,
            result.LegalEntityId);
        Assert.Equal(
            businessUnitId,
            result.BusinessUnitId);
        Assert.Equal(
            "SELECTED",
            account.AccountNumber);
        Assert.Equal(
            400m,
            result.TotalBalanceInBaseCurrency);
    }

    private static Account CreateAccount(
        AccountType accountType,
        Guid legalEntityId,
        Guid businessUnitId,
        string accountNumber,
        decimal balance)
    {
        return new Account
        {
            Id = Guid.NewGuid(),
            LegalEntityId = legalEntityId,
            BusinessUnitId = businessUnitId,
            Name = accountNumber,
            AccountNumber = accountNumber,
            Currency = "NGN",
            Balance = balance,
            IsActive = true,
            AccountTypeId = accountType.Id,
            AccountType = accountType
        };
    }
}

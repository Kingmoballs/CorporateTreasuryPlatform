using Moq;
using Treasury.Application.Common.Exceptions;
using Treasury.Application.DTOs.Accounts;
using Treasury.Application.DTOs.Audit;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Services;

namespace Treasury.Tests.Accounts;

public class AccountOrganizationDimensionTests
{
    [Fact]
    public async Task
        GetAccountTypes_ReturnsAvailableTypes()
    {
        var setup = CreateSetup();
        var accountTypes =
            new List<AccountType>
            {
                new()
                {
                    Id = setup.AccountTypeId,
                    Name = "Operating"
                }
            };

        setup.AccountTypes
            .Setup(item => item.GetAll())
            .ReturnsAsync(accountTypes);

        var result =
            await setup.Service.GetAccountTypes();

        var accountType = Assert.Single(result);

        Assert.Equal(
            setup.AccountTypeId,
            accountType.Id);
        Assert.Equal(
            "Operating",
            accountType.Name);
    }

    [Fact]
    public async Task
        CreateAccount_AssignsSelectedOrganizationDimensions()
    {
        var setup = CreateSetup();
        var legalEntity =
            CreateLegalEntity(setup.OrganizationId);
        var businessUnit =
            CreateBusinessUnit(
                setup.OrganizationId,
                legalEntity.Id);
        Account? addedAccount = null;

        setup.OrganizationStructure
            .Setup(item =>
                item.GetLegalEntity(legalEntity.Id))
            .ReturnsAsync(legalEntity);

        setup.OrganizationStructure
            .Setup(item =>
                item.GetBusinessUnit(businessUnit.Id))
            .ReturnsAsync(businessUnit);

        setup.Accounts
            .Setup(item =>
                item.Add(It.IsAny<Account>()))
            .Callback<Account>(
                account => addedAccount = account)
            .Returns(Task.CompletedTask);

        var result =
            await setup.Service.CreateAccount(
                CreateRequest(
                    setup.AccountTypeId,
                    legalEntity.Id,
                    businessUnit.Id));

        Assert.NotNull(addedAccount);
        Assert.Equal(
            setup.OrganizationId,
            addedAccount.OrganizationId);
        Assert.Equal(
            legalEntity.Id,
            addedAccount.LegalEntityId);
        Assert.Equal(
            businessUnit.Id,
            addedAccount.BusinessUnitId);
        Assert.Equal(
            legalEntity.Code,
            result.LegalEntityCode);
        Assert.Equal(
            businessUnit.Code,
            result.BusinessUnitCode);

        setup.Accounts.Verify(
            item => item.CommitTransaction(),
            Times.Once);
        setup.AuditLogs.Verify(
            item => item.Record(
                It.IsAny<CreateAuditLogDto>()),
            Times.Once);
    }

    [Fact]
    public async Task
        CreateAccount_BusinessUnitFromAnotherEntityIsRejected()
    {
        var setup = CreateSetup();
        var selectedEntity =
            CreateLegalEntity(setup.OrganizationId);
        var otherEntity =
            CreateLegalEntity(setup.OrganizationId);
        var businessUnit =
            CreateBusinessUnit(
                setup.OrganizationId,
                otherEntity.Id);

        setup.OrganizationStructure
            .Setup(item =>
                item.GetLegalEntity(selectedEntity.Id))
            .ReturnsAsync(selectedEntity);

        setup.OrganizationStructure
            .Setup(item =>
                item.GetBusinessUnit(businessUnit.Id))
            .ReturnsAsync(businessUnit);

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => setup.Service.CreateAccount(
                CreateRequest(
                    setup.AccountTypeId,
                    selectedEntity.Id,
                    businessUnit.Id)));

        setup.Accounts.Verify(
            item => item.BeginTransaction(),
            Times.Never);
        setup.Accounts.Verify(
            item => item.Add(It.IsAny<Account>()),
            Times.Never);
    }

    [Fact]
    public async Task
        GetAccounts_AppliesBothOrganizationDimensions()
    {
        var setup = CreateSetup();
        var legalEntityId = Guid.NewGuid();
        var businessUnitId = Guid.NewGuid();
        var accountType =
            new AccountType
            {
                Id = setup.AccountTypeId,
                Name = "Operating"
            };

        setup.Accounts
            .Setup(item => item.GetAll())
            .ReturnsAsync(
                new List<Account>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        OrganizationId =
                            setup.OrganizationId,
                        LegalEntityId =
                            legalEntityId,
                        BusinessUnitId =
                            businessUnitId,
                        Name = "Selected Account",
                        AccountNumber = "SELECTED-001",
                        AccountTypeId =
                            accountType.Id,
                        AccountType = accountType,
                        Currency = "NGN"
                    },
                    new()
                    {
                        Id = Guid.NewGuid(),
                        OrganizationId =
                            setup.OrganizationId,
                        LegalEntityId =
                            legalEntityId,
                        BusinessUnitId =
                            Guid.NewGuid(),
                        Name = "Other Account",
                        AccountNumber = "OTHER-001",
                        AccountTypeId =
                            accountType.Id,
                        AccountType = accountType,
                        Currency = "NGN"
                    }
                });

        var result =
            await setup.Service.GetAccounts(
                legalEntityId,
                businessUnitId);

        var account = Assert.Single(result);

        Assert.Equal(
            "SELECTED-001",
            account.AccountNumber);
        Assert.Equal(
            legalEntityId,
            account.LegalEntityId);
        Assert.Equal(
            businessUnitId,
            account.BusinessUnitId);
    }

    [Fact]
    public async Task
        GetAccounts_EmptyDimensionIdIsRejected()
    {
        var setup = CreateSetup();

        await Assert.ThrowsAsync<ArgumentException>(
            () => setup.Service.GetAccounts(
                Guid.Empty,
                null));

        setup.Accounts.Verify(
            item => item.GetAll(),
            Times.Never);
    }

    private static ServiceSetup CreateSetup()
    {
        var organizationId = Guid.NewGuid();
        var accountType =
            new AccountType
            {
                Id = Guid.NewGuid(),
                Name = "Operating"
            };

        var accounts =
            new Mock<IAccountRepository>();
        var accountTypes =
            new Mock<IAccountTypeRepository>();
        var organizationStructure =
            new Mock<
                IOrganizationStructureRepository>();
        var currentUser =
            new Mock<ICurrentUserService>();
        var auditLogs =
            new Mock<IAuditLogService>();

        accountTypes
            .Setup(item =>
                item.GetById(accountType.Id))
            .ReturnsAsync(accountType);
        accounts
            .Setup(item =>
                item.AccountNumberExists(
                    It.IsAny<string>()))
            .ReturnsAsync(false);
        accounts
            .Setup(item => item.BeginTransaction())
            .Returns(Task.CompletedTask);
        accounts
            .Setup(item => item.SaveChanges())
            .Returns(Task.CompletedTask);
        accounts
            .Setup(item => item.CommitTransaction())
            .Returns(Task.CompletedTask);
        accounts
            .Setup(item => item.RollbackTransaction())
            .Returns(Task.CompletedTask);
        currentUser
            .SetupGet(item => item.OrganizationId)
            .Returns(organizationId);
        auditLogs
            .Setup(item => item.Record(
                It.IsAny<CreateAuditLogDto>()))
            .Returns(Task.CompletedTask);

        var service =
            new AccountService(
                accounts.Object,
                accountTypes.Object,
                organizationStructure.Object,
                new Mock<ILedgerRepository>().Object,
                new Mock<
                    ITreasuryTransactionRepository>()
                    .Object,
                currentUser.Object,
                auditLogs.Object);

        return new ServiceSetup(
            service,
            accounts,
            accountTypes,
            organizationStructure,
            auditLogs,
            organizationId,
            accountType.Id);
    }

    private static CreateAccountDto CreateRequest(
        Guid accountTypeId,
        Guid legalEntityId,
        Guid? businessUnitId)
    {
        return new CreateAccountDto
        {
            Name = "Operating Account",
            AccountNumber = "OPS-001",
            AccountTypeId = accountTypeId,
            LegalEntityId = legalEntityId,
            BusinessUnitId = businessUnitId,
            Currency = "NGN",
            OpeningBalance = 0
        };
    }

    private static LegalEntity CreateLegalEntity(
        Guid organizationId)
    {
        return new LegalEntity
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Code = $"LE-{Guid.NewGuid():N}",
            Name = "Legal Entity",
            IsActive = true
        };
    }

    private static BusinessUnit CreateBusinessUnit(
        Guid organizationId,
        Guid legalEntityId)
    {
        return new BusinessUnit
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            LegalEntityId = legalEntityId,
            Code = $"BU-{Guid.NewGuid():N}",
            Name = "Business Unit",
            IsActive = true
        };
    }

    private sealed record ServiceSetup(
        AccountService Service,
        Mock<IAccountRepository> Accounts,
        Mock<IAccountTypeRepository> AccountTypes,
        Mock<IOrganizationStructureRepository>
            OrganizationStructure,
        Mock<IAuditLogService> AuditLogs,
        Guid OrganizationId,
        Guid AccountTypeId);
}

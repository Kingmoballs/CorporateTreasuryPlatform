using Microsoft.EntityFrameworkCore;
using Treasury.Application.Common;
using Treasury.Application.DTOs.BankStatements;
using Treasury.Application.DTOs.InvestmentPlacements;
using Treasury.Application.DTOs.Transactions;
using Treasury.Application.DTOs.TreasuryAlerts;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Repositories;
using Treasury.Shared.Constants;

namespace Treasury.Tests.Integration;

public class AccountOrganizationDimensionIntegrationTests
{
    [Fact]
    public async Task
        AccountDimensionsRequireMatchingTenantAndEntity()
    {
        await using var database =
            await PostgreSqlTestDatabase.Start();

        Guid organizationId;
        Guid accountTypeId;
        Guid selectedLegalEntityId;
        Guid selectedBusinessUnitId;
        Guid otherLegalEntityId;
        Guid otherBusinessUnitId;

        await using (var context =
            database.CreateContext())
        {
            organizationId =
                await context.Organizations
                    .Select(item => item.Id)
                    .SingleAsync();

            var accountType =
                new AccountType
                {
                    Id = Guid.NewGuid(),
                    Name = "Dimension Test Account"
                };

            accountTypeId = accountType.Id;

            var selectedLegalEntity =
                new LegalEntity
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = organizationId,
                    Code = "ENTITY-SELECTED",
                    Name = "Selected Entity",
                    CountryCode = "NG",
                    BaseCurrency = "NGN"
                };

            var selectedBusinessUnit =
                new BusinessUnit
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = organizationId,
                    LegalEntityId =
                        selectedLegalEntity.Id,
                    Code = "UNIT-SELECTED",
                    Name = "Selected Unit"
                };

            var otherLegalEntity =
                new LegalEntity
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = organizationId,
                    Code = "ENTITY-OTHER",
                    Name = "Other Entity",
                    CountryCode = "NG",
                    BaseCurrency = "NGN"
                };

            var otherBusinessUnit =
                new BusinessUnit
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = organizationId,
                    LegalEntityId =
                        otherLegalEntity.Id,
                    Code = "UNIT-OTHER",
                    Name = "Other Unit"
                };

            await context.LegalEntities.AddRangeAsync(
                selectedLegalEntity,
                otherLegalEntity);
            await context.BusinessUnits.AddRangeAsync(
                selectedBusinessUnit,
                otherBusinessUnit);
            await context.AccountTypes.AddAsync(
                accountType);
            await context.SaveChangesAsync();

            selectedLegalEntityId =
                selectedLegalEntity.Id;
            selectedBusinessUnitId =
                selectedBusinessUnit.Id;
            otherLegalEntityId =
                otherLegalEntity.Id;
            otherBusinessUnitId =
                otherBusinessUnit.Id;
        }

        await using (var context =
            database.CreateContext(organizationId))
        {
            await context.Accounts.AddAsync(
                CreateAccount(
                    organizationId,
                    accountTypeId,
                    selectedLegalEntityId,
                    otherBusinessUnitId,
                    "INVALID-DIMENSIONS"));

            await Assert.ThrowsAsync<
                DbUpdateException>(
                () => context.SaveChangesAsync());
        }

        var validAccount =
            CreateAccount(
                organizationId,
                accountTypeId,
                selectedLegalEntityId,
                selectedBusinessUnitId,
                "VALID-DIMENSIONS");

        var otherValidAccount =
            CreateAccount(
                organizationId,
                accountTypeId,
                otherLegalEntityId,
                otherBusinessUnitId,
                "OTHER-VALID-DIMENSIONS");

        await using (var context =
            database.CreateContext(organizationId))
        {
            await context.Accounts.AddRangeAsync(
                validAccount,
                otherValidAccount);
            await context.SaveChangesAsync();

            await context.TreasuryTransactions
                .AddRangeAsync(
                    CreateTransaction(
                        organizationId,
                        "TXN-SELECTED",
                        TransactionTypes.CashReceipt,
                        sourceAccountId: null,
                        destinationAccountId:
                            validAccount.Id),
                    CreateTransaction(
                        organizationId,
                        "TXN-CROSS-SCOPE",
                        TransactionTypes
                            .InternalTransfer,
                        sourceAccountId:
                            otherValidAccount.Id,
                        destinationAccountId:
                            validAccount.Id),
                    CreateTransaction(
                        organizationId,
                        "TXN-OTHER",
                        TransactionTypes.CashReceipt,
                        sourceAccountId: null,
                        destinationAccountId:
                            otherValidAccount.Id));

            await context.BankStatementImports
                .AddRangeAsync(
                    CreateStatementImport(
                        organizationId,
                        validAccount,
                        "STATEMENT-SELECTED"),
                    CreateStatementImport(
                        organizationId,
                        otherValidAccount,
                        "STATEMENT-OTHER"));

            await context.TreasuryAlerts
                .AddRangeAsync(
                    CreateAlert(
                        organizationId,
                        validAccount.Id,
                        "ALERT-SELECTED"),
                    CreateAlert(
                        organizationId,
                        otherValidAccount.Id,
                        "ALERT-OTHER"),
                    CreateAlert(
                        organizationId,
                        accountId: null,
                        "ALERT-ORGANIZATION-WIDE"));

            await context.InvestmentPlacements
                .AddRangeAsync(
                    CreateInvestmentPlacement(
                        organizationId,
                        validAccount,
                        "INVESTMENT-SELECTED"),
                    CreateInvestmentPlacement(
                        organizationId,
                        otherValidAccount,
                        "INVESTMENT-OTHER"));
            await context.SaveChangesAsync();
        }

        await using (var context =
            database.CreateContext(organizationId))
        {
            var account =
                await context.Accounts
                    .Include(item =>
                        item.LegalEntity)
                    .Include(item =>
                        item.BusinessUnit)
                    .SingleAsync(item =>
                        item.Id ==
                            validAccount.Id);

            Assert.Equal(
                "ENTITY-SELECTED",
                account.LegalEntity?.Code);
            Assert.Equal(
                "UNIT-SELECTED",
                account.BusinessUnit?.Code);

            var filteredAccounts =
                OrganizationDimensionFilter
                    .Create(
                        selectedLegalEntityId,
                        selectedBusinessUnitId)
                    .Apply(
                        await context.Accounts
                            .AsNoTracking()
                            .ToListAsync());

            var filteredAccount =
                Assert.Single(filteredAccounts);

            Assert.Equal(
                validAccount.Id,
                filteredAccount.Id);

            var transactionRepository =
                new TreasuryTransactionRepository(
                    context);
            var transactionQuery =
                new TransactionQueryDto
                {
                    LegalEntityId =
                        selectedLegalEntityId,
                    BusinessUnitId =
                        selectedBusinessUnitId,
                    Page = 1,
                    PageSize = 20
                };

            var search =
                await transactionRepository.Search(
                    transactionQuery);
            var export =
                await transactionRepository
                    .GetForExport(
                        transactionQuery,
                        20);
            var activity =
                await transactionRepository
                    .GetForActivitySummary(
                        new
                            TreasuryActivitySummaryQueryDto
                            {
                                LegalEntityId =
                                    selectedLegalEntityId,
                                BusinessUnitId =
                                    selectedBusinessUnitId
                            });

            Assert.Equal(2, search.TotalCount);
            Assert.Equal(2, export.Count);
            Assert.Equal(2, activity.Count);
            Assert.Contains(
                search.Items,
                item =>
                    item.Reference ==
                        "TXN-SELECTED");
            Assert.Contains(
                search.Items,
                item =>
                    item.Reference ==
                        "TXN-CROSS-SCOPE");
            Assert.DoesNotContain(
                search.Items,
                item =>
                    item.Reference ==
                        "TXN-OTHER");

            var bankStatementRepository =
                new BankStatementRepository(
                    context);
            var unmatchedLines =
                await bankStatementRepository
                    .GetUnmatchedLines(
                        accountId: null,
                        fromUtc: null,
                        toUtc: null,
                        legalEntityId:
                            selectedLegalEntityId,
                        businessUnitId:
                            selectedBusinessUnitId);
            var unmatchedLine =
                Assert.Single(unmatchedLines);

            Assert.Equal(
                "STATEMENT-SELECTED",
                unmatchedLine.Description);
            Assert.Equal(
                selectedLegalEntityId,
                unmatchedLine.Account.LegalEntityId);
            Assert.Equal(
                selectedBusinessUnitId,
                unmatchedLine.Account.BusinessUnitId);

            var alertRepository =
                new TreasuryAlertRepository(
                    context);
            var alertQuery =
                new TreasuryAlertQueryDto
                {
                    LegalEntityId =
                        selectedLegalEntityId,
                    BusinessUnitId =
                        selectedBusinessUnitId,
                    Page = 1,
                    PageSize = 20
                };
            var alertSearch =
                await alertRepository.Search(
                    alertQuery);
            var alertSummary =
                await alertRepository.GetForSummary(
                    new TreasuryAlertSummaryQueryDto
                    {
                        LegalEntityId =
                            selectedLegalEntityId,
                        BusinessUnitId =
                            selectedBusinessUnitId
                    });
            var alertExport =
                await alertRepository.GetForExport(
                    alertQuery,
                    20);

            Assert.Equal(1, alertSearch.TotalCount);
            Assert.Single(alertSummary);
            Assert.Single(alertExport);
            Assert.Equal(
                "ALERT-SELECTED",
                Assert.Single(
                    alertSearch.Items)
                    .Title);
            Assert.DoesNotContain(
                alertSearch.Items,
                item =>
                    item.Title ==
                        "ALERT-ORGANIZATION-WIDE");

            var investmentRepository =
                new InvestmentPlacementRepository(
                    context);
            var investmentQuery =
                new InvestmentPlacementQueryDto
                {
                    LegalEntityId =
                        selectedLegalEntityId,
                    BusinessUnitId =
                        selectedBusinessUnitId,
                    Page = 1,
                    PageSize = 20
                };
            var investmentSearch =
                await investmentRepository.Search(
                    investmentQuery);
            var investmentReport =
                await investmentRepository
                    .GetForReporting(
                        new InvestmentPortfolioQueryDto
                        {
                            LegalEntityId =
                                selectedLegalEntityId,
                            BusinessUnitId =
                                selectedBusinessUnitId
                        });

            Assert.Equal(
                1,
                investmentSearch.TotalCount);
            Assert.Single(investmentReport);
            Assert.Equal(
                "INVESTMENT-SELECTED",
                Assert.Single(
                    investmentSearch.Items)
                    .Reference);
            Assert.Equal(
                selectedLegalEntityId,
                Assert.Single(investmentReport)
                    .SourceAccount
                    .LegalEntityId);
        }
    }

    private static Account CreateAccount(
        Guid organizationId,
        Guid accountTypeId,
        Guid legalEntityId,
        Guid businessUnitId,
        string accountNumber)
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
            AccountTypeId = accountTypeId
        };
    }

    private static TreasuryTransaction
        CreateTransaction(
            Guid organizationId,
            string reference,
            string transactionType,
            Guid? sourceAccountId,
            Guid? destinationAccountId)
    {
        return new TreasuryTransaction
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Reference = reference,
            TransactionType = transactionType,
            Status =
                TransactionStatuses.Completed,
            Amount = 100m,
            Currency = "NGN",
            Description = reference,
            SourceAccountId = sourceAccountId,
            DestinationAccountId =
                destinationAccountId,
            CreatedAtUtc = DateTime.UtcNow,
            CompletedAtUtc = DateTime.UtcNow
        };
    }

    private static BankStatementImport
        CreateStatementImport(
            Guid organizationId,
            Account account,
            string description)
    {
        var statementImport =
            new BankStatementImport
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                AccountId = account.Id,
                FileName =
                    $"{description}.csv",
                Currency = account.Currency,
                LineCount = 1,
                UploadedAtUtc = DateTime.UtcNow
            };

        statementImport.Lines.Add(
            new BankStatementLine
            {
                Id = Guid.NewGuid(),
                OrganizationId =
                    organizationId,
                BankStatementImportId =
                    statementImport.Id,
                AccountId = account.Id,
                LineNumber = 1,
                TransactionDateUtc =
                    DateTime.UtcNow,
                Description = description,
                Amount = 100m,
                Currency = account.Currency,
                ReconciliationStatus =
                    ReconciliationStatus.Unmatched,
                CreatedAtUtc = DateTime.UtcNow
            });

        return statementImport;
    }

    private static TreasuryAlert CreateAlert(
        Guid organizationId,
        Guid? accountId,
        string title)
    {
        return new TreasuryAlert
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            AlertType =
                TreasuryAlertTypes.LowLiquidity,
            Severity =
                TreasuryAlertSeverities.Warning,
            Status =
                TreasuryAlertStatuses.Open,
            Title = title,
            Message = title,
            AccountId = accountId,
            Currency = "NGN",
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    private static InvestmentPlacement
        CreateInvestmentPlacement(
            Guid organizationId,
            Account account,
            string reference)
    {
        var startDateUtc =
            DateTime.UtcNow.Date;

        return new InvestmentPlacement
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Reference = reference,
            InvestmentType =
                InvestmentPlacementTypes.FixedDeposit,
            InstitutionName = "Dimension Bank",
            SourceAccountId = account.Id,
            PrincipalAmount = 1_000m,
            Currency = account.Currency,
            AnnualInterestRate = 10m,
            DayCountBasis = 365,
            StartDateUtc = startDateUtc,
            MaturityDateUtc =
                startDateUtc.AddDays(30),
            ExpectedInterestAmount = 10m,
            ExpectedMaturityAmount = 1_010m,
            Status =
                InvestmentPlacementStatuses.Active,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }
}

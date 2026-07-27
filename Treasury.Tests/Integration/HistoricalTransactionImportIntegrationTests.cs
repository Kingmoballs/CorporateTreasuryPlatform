using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Treasury.Application.Common.Exceptions;
using Treasury.Application.DTOs.Audit;
using Treasury.Application.DTOs.HistoricalImports;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Repositories;
using Treasury.Infrastructure.Services;
using Treasury.Shared.Constants;

namespace Treasury.Tests.Integration;

public class HistoricalTransactionImportIntegrationTests
{
    [Fact]
    public async Task
        DryRunPersistsTenantStagingWithoutFinancialPosting()
    {
        await using var database =
            await PostgreSqlTestDatabase.Start();

        Guid organizationId;
        Guid userId;
        Guid accountId;
        var initialBalance = 321_000m;
        var initialReservedBalance = 20_000m;

        await using (var context =
            database.CreateContext())
        {
            organizationId =
                context.CurrentOrganizationId!.Value;

            var role =
                new Role
                {
                    Id = Guid.NewGuid(),
                    Name =
                        $"HistoricalImportRole-" +
                        $"{Guid.NewGuid():N}"
                };

            var user =
                new User
                {
                    Id = Guid.NewGuid(),
                    FirstName = "Historical",
                    LastName = "Importer",
                    Email =
                        $"historical-{Guid.NewGuid():N}" +
                        "@example.com",
                    PasswordHash = "not-used",
                    RoleId = role.Id,
                    Role = role,
                    IsActive = true
                };

            var accountType =
                new AccountType
                {
                    Id = Guid.NewGuid(),
                    Name =
                        $"Historical Import Account-" +
                        $"{Guid.NewGuid():N}"
                };

            var legalEntity =
                new LegalEntity
                {
                    Id = Guid.NewGuid(),
                    OrganizationId =
                        organizationId,
                    Code = "HIST-LE",
                    Name =
                        "Historical Import Legal Entity",
                    CountryCode = "NG",
                    BaseCurrency = "NGN"
                };

            var businessUnit =
                new BusinessUnit
                {
                    Id = Guid.NewGuid(),
                    OrganizationId =
                        organizationId,
                    LegalEntityId =
                        legalEntity.Id,
                    LegalEntity = legalEntity,
                    Code = "HIST-BU",
                    Name =
                        "Historical Import Business Unit"
                };

            var account =
                new Account
                {
                    Id = Guid.NewGuid(),
                    OrganizationId =
                        organizationId,
                    LegalEntityId =
                        legalEntity.Id,
                    LegalEntity = legalEntity,
                    BusinessUnitId =
                        businessUnit.Id,
                    BusinessUnit = businessUnit,
                    Name =
                        "Historical Import Account",
                    AccountNumber = "HIST-100001",
                    Balance = initialBalance,
                    ReservedBalance =
                        initialReservedBalance,
                    Currency = "NGN",
                    IsActive = true,
                    AccountTypeId = accountType.Id,
                    AccountType = accountType
                };

            userId = user.Id;
            accountId = account.Id;

            await context.Roles.AddAsync(role);
            await context.Users.AddAsync(user);
            await context.AccountTypes.AddAsync(
                accountType);
            await context.LegalEntities.AddAsync(
                legalEntity);
            await context.BusinessUnits.AddAsync(
                businessUnit);
            await context.Accounts.AddAsync(account);
            await context.SaveChangesAsync();
        }

        Guid batchId;

        await using (var context =
            database.CreateContext(organizationId))
        {
            var currentUser =
                new Mock<ICurrentUserService>();
            currentUser
                .SetupGet(item => item.UserId)
                .Returns(userId);
            currentUser
                .SetupGet(item =>
                    item.OrganizationId)
                .Returns(organizationId);

            var audit =
                new Mock<IAuditLogService>();
            audit
                .Setup(item =>
                    item.Record(
                        It.IsAny<
                            CreateAuditLogDto>()))
                .Returns(Task.CompletedTask);

            var service =
                new HistoricalTransactionImportService(
                    new
                        HistoricalTransactionImportRepository(
                            context),
                    currentUser.Object,
                    audit.Object,
                    Options.Create(
                        new HistoricalImportOptions()),
                    TimeProvider.System);

            var csv =
                "ExternalReference,AccountNumber," +
                "LegalEntityCode,BusinessUnitCode," +
                "TransactionDateUtc,ValueDateUtc," +
                "Amount,Currency,Direction," +
                "TransactionType,Description,Category," +
                "CounterpartyName\r\n" +
                "LEGACY-001,HIST-100001,HIST-LE," +
                "HIST-BU,2025-04-10T10:30:00Z," +
                "2025-04-10T10:30:00Z,2500.25,NGN," +
                "Credit,CustomerReceipt,Legacy receipt," +
                "Receipts,Legacy customer";

            var result = await service.DryRun(
                new CreateHistoricalImportDryRunDto
                {
                    ImportKey = Guid.NewGuid(),
                    Mode =
                        HistoricalImportModes
                            .HistoricalTransactions,
                    FileName = "legacy.csv",
                    FileContent =
                        Encoding.UTF8.GetBytes(csv)
                });

            batchId = result.Id;

            Assert.Equal(
                HistoricalImportStatuses.Validated,
                result.Status);
            Assert.False(result.IsPostingOperation);

            var savedBatch =
                await context
                    .HistoricalTransactionImportBatches
                    .Include(batch => batch.Rows)
                    .SingleAsync(batch =>
                        batch.Id == batchId);

            Assert.Equal(
                organizationId,
                savedBatch.OrganizationId);
            Assert.Single(savedBatch.Rows);
            Assert.True(
                savedBatch.Rows.Single().IsValid);

            var account =
                await context.Accounts
                    .SingleAsync(item =>
                        item.Id == accountId);

            Assert.Equal(
                initialBalance,
                account.Balance);
            Assert.Equal(
                initialReservedBalance,
                account.ReservedBalance);
            Assert.Empty(
                await context.TreasuryTransactions
                    .Where(transaction =>
                        transaction.SourceAccountId ==
                            accountId ||
                        transaction
                            .DestinationAccountId ==
                                accountId)
                    .ToListAsync());
            Assert.Empty(
                await context.LedgerEntries
                    .Where(entry =>
                        entry.AccountId == accountId)
                    .ToListAsync());
        }

        await using var noTenantContext =
            database
                .CreateContextWithoutOrganization();

        Assert.Null(
            await noTenantContext
                .HistoricalTransactionImportBatches
                .FirstOrDefaultAsync(batch =>
                    batch.Id == batchId));
    }

    [Fact]
    public async Task
        CutoverRequiresAdminAndCfoThenPostsExactlyOnce()
    {
        await using var database =
            await PostgreSqlTestDatabase.Start();

        Guid organizationId;
        Guid uploaderId;
        Guid adminId;
        Guid cfoId;
        Guid accountId;

        await using (var context =
            database.CreateContext())
        {
            organizationId =
                context.CurrentOrganizationId!.Value;

            var uploaderRole =
                new Role
                {
                    Id = Guid.NewGuid(),
                    Name = Roles.TreasuryOfficer
                };
            var adminRole =
                new Role
                {
                    Id = Guid.NewGuid(),
                    Name = Roles.Admin
                };
            var cfoRole =
                new Role
                {
                    Id = Guid.NewGuid(),
                    Name = Roles.CFO
                };

            var uploader = CreateUser(
                uploaderRole,
                "uploader");
            var admin = CreateUser(
                adminRole,
                "admin");
            var cfo = CreateUser(
                cfoRole,
                "cfo");
            var accountType =
                new AccountType
                {
                    Id = Guid.NewGuid(),
                    Name =
                        $"Cutover Account-" +
                        $"{Guid.NewGuid():N}"
                };
            var account =
                new Account
                {
                    Id = Guid.NewGuid(),
                    OrganizationId =
                        organizationId,
                    Name = "Cutover Account",
                    AccountNumber = "CUTOVER-100001",
                    Balance = 0,
                    ReservedBalance = 0,
                    Currency = "NGN",
                    IsActive = true,
                    AccountTypeId = accountType.Id,
                    AccountType = accountType
                };

            uploaderId = uploader.Id;
            adminId = admin.Id;
            cfoId = cfo.Id;
            accountId = account.Id;

            await context.Roles.AddRangeAsync(
                uploaderRole,
                adminRole,
                cfoRole);
            await context.Users.AddRangeAsync(
                uploader,
                admin,
                cfo);
            await context.AccountTypes.AddAsync(
                accountType);
            await context.Accounts.AddAsync(account);
            await context.SaveChangesAsync();
        }

        await using var tenantContext =
            database.CreateContext(organizationId);

        var repository =
            new HistoricalTransactionImportRepository(
                tenantContext);
        var audit = new Mock<IAuditLogService>();
        audit
            .Setup(item =>
                item.Record(
                    It.IsAny<CreateAuditLogDto>()))
            .Returns(Task.CompletedTask);

        var uploaderService = CreateService(
            repository,
            audit.Object,
            organizationId,
            uploaderId,
            Roles.TreasuryOfficer);
        var adminService = CreateService(
            repository,
            audit.Object,
            organizationId,
            adminId,
            Roles.Admin);
        var cfoService = CreateService(
            repository,
            audit.Object,
            organizationId,
            cfoId,
            Roles.CFO);

        var csv =
            "ExternalReference,AccountNumber," +
            "LegalEntityCode,BusinessUnitCode," +
            "CutoverDateUtc,OpeningBalance,Currency," +
            "Description\r\n" +
            "OPEN-001,CUTOVER-100001,,,2025-01-01," +
            "750000,NGN,Cutover opening balance";

        var dryRun = await uploaderService.DryRun(
            new CreateHistoricalImportDryRunDto
            {
                ImportKey = Guid.NewGuid(),
                Mode =
                    HistoricalImportModes
                        .CutoverOpeningBalances,
                FileName = "cutover.csv",
                FileContent =
                    Encoding.UTF8.GetBytes(csv)
            });

        var submitted = await uploaderService.Submit(
            dryRun.Id,
            new HistoricalImportConcurrencyDto
            {
                ConcurrencyToken =
                    dryRun.ConcurrencyToken
            });

        var adminApproval =
            await adminService.Approve(
                dryRun.Id,
                new ReviewHistoricalImportDto
                {
                    ConcurrencyToken =
                        submitted.ConcurrencyToken
                });

        Assert.Equal(
            HistoricalImportStatuses
                .PendingApproval,
            adminApproval.Status);

        var cfoApproval = await cfoService.Approve(
            dryRun.Id,
            new ReviewHistoricalImportDto
            {
                ConcurrencyToken =
                    adminApproval.ConcurrencyToken
            });

        Assert.Equal(
            HistoricalImportStatuses.Approved,
            cfoApproval.Status);

        var committed = await adminService.Commit(
            dryRun.Id,
            new HistoricalImportConcurrencyDto
            {
                ConcurrencyToken =
                    cfoApproval.ConcurrencyToken
            });

        Assert.Equal(
            HistoricalImportStatuses.Committed,
            committed.Batch.Status);
        Assert.Equal(
            1,
            committed.OpeningBalancePostingCount);

        tenantContext.ChangeTracker.Clear();

        var accountAfter =
            await tenantContext.Accounts
                .SingleAsync(account =>
                    account.Id == accountId);
        var transaction =
            await tenantContext
                .TreasuryTransactions
                .SingleAsync(item =>
                    item.DestinationAccountId ==
                        accountId);
        var ledgerEntry =
            await tenantContext.LedgerEntries
                .SingleAsync(item =>
                    item.AccountId == accountId);
        var decisions =
            await tenantContext
                .HistoricalTransactionImportDecisions
                .Where(item =>
                    item.BatchId == dryRun.Id)
                .ToListAsync();

        Assert.Equal(750_000m, accountAfter.Balance);
        Assert.Equal(
            TransactionTypes.OpeningBalance,
            transaction.TransactionType);
        Assert.Equal(
            TransactionStatuses.Completed,
            transaction.Status);
        Assert.Equal(
            transaction.Id,
            ledgerEntry.TreasuryTransactionId);
        Assert.Equal("Debit", ledgerEntry.EntryType);
        Assert.Equal(2, decisions.Count);
        Assert.Contains(
            decisions,
            item => item.ApproverRole == Roles.Admin);
        Assert.Contains(
            decisions,
            item => item.ApproverRole == Roles.CFO);
        Assert.Empty(
            await tenantContext
                .HistoricalTransactionRecords
                .ToListAsync());

        await Assert.ThrowsAsync<ConflictException>(
            () => adminService.Commit(
                dryRun.Id,
                new HistoricalImportConcurrencyDto
                {
                    ConcurrencyToken =
                        committed.Batch
                            .ConcurrencyToken
                }));
    }

    private static User CreateUser(
        Role role,
        string prefix)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            FirstName = prefix,
            LastName = "Reviewer",
            Email =
                $"{prefix}-{Guid.NewGuid():N}" +
                "@example.com",
            PasswordHash = "not-used",
            RoleId = role.Id,
            Role = role,
            IsActive = true
        };
    }

    private static
        HistoricalTransactionImportService CreateService(
            IHistoricalTransactionImportRepository
                repository,
            IAuditLogService audit,
            Guid organizationId,
            Guid userId,
            string role)
    {
        var currentUser =
            new Mock<ICurrentUserService>();
        currentUser
            .SetupGet(item => item.UserId)
            .Returns(userId);
        currentUser
            .SetupGet(item => item.OrganizationId)
            .Returns(organizationId);
        currentUser
            .SetupGet(item => item.Role)
            .Returns(role);

        return new HistoricalTransactionImportService(
            repository,
            currentUser.Object,
            audit,
            Options.Create(
                new HistoricalImportOptions()),
            TimeProvider.System);
    }
}

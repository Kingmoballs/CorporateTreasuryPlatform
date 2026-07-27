using Microsoft.Extensions.Options;
using Moq;
using Treasury.Application.Common.Exceptions;
using Treasury.Application.DTOs.Audit;
using Treasury.Application.DTOs.HistoricalImports;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Services;
using Treasury.Shared.Constants;

namespace Treasury.Tests.HistoricalImports;

public class HistoricalTransactionImportApprovalTests
{
    private readonly Guid _organizationId =
        Guid.NewGuid();

    [Fact]
    public async Task Submit_ValidatedBatch_RequiresIndependentApproval()
    {
        var uploaderId = Guid.NewGuid();
        var batch = CreateBatch(
            HistoricalImportModes
                .HistoricalTransactions,
            HistoricalImportStatuses.Validated,
            uploaderId);
        var repository = CreateRepository(batch);
        var service = CreateService(
            repository.Object,
            uploaderId,
            Roles.TreasuryOfficer);

        var result = await service.Submit(
            batch.Id,
            new HistoricalImportConcurrencyDto
            {
                ConcurrencyToken =
                    batch.ConcurrencyToken
            });

        Assert.Equal(
            HistoricalImportStatuses
                .PendingApproval,
            result.Status);
        Assert.Equal(1, result.RequiredApprovalCount);
        Assert.Equal(0, result.ApprovalCount);
        Assert.Equal(uploaderId, result.SubmittedByUserId);
        repository.Verify(
            item => item.CommitTransaction(),
            Times.Once);
    }

    [Fact]
    public async Task Approve_UploaderReview_IsForbidden()
    {
        var uploaderId = Guid.NewGuid();
        var batch = CreateBatch(
            HistoricalImportModes
                .HistoricalTransactions,
            HistoricalImportStatuses
                .PendingApproval,
            uploaderId);
        batch.SubmittedByUserId = uploaderId;
        batch.SubmittedAtUtc = DateTime.UtcNow;
        batch.RequiredApprovalCount = 1;

        var service = CreateService(
            CreateRepository(batch).Object,
            uploaderId,
            Roles.Admin);

        await Assert.ThrowsAsync<
            ForbiddenOperationException>(
            () => service.Approve(
                batch.Id,
                new ReviewHistoricalImportDto
                {
                    ConcurrencyToken =
                        batch.ConcurrencyToken
                }));
    }

    [Fact]
    public async Task
        CutoverApproval_RequiresOneAdminAndOneCfo()
    {
        var uploaderId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var cfoId = Guid.NewGuid();
        var batch = CreateBatch(
            HistoricalImportModes
                .CutoverOpeningBalances,
            HistoricalImportStatuses
                .PendingApproval,
            uploaderId);
        batch.SubmittedByUserId = uploaderId;
        batch.SubmittedAtUtc = DateTime.UtcNow;
        batch.RequiredApprovalCount = 2;

        var repository = CreateRepository(batch);

        var adminService = CreateService(
            repository.Object,
            adminId,
            Roles.Admin);

        var first = await adminService.Approve(
            batch.Id,
            new ReviewHistoricalImportDto
            {
                ConcurrencyToken =
                    batch.ConcurrencyToken,
                Comment = "Admin approval"
            });

        Assert.Equal(
            HistoricalImportStatuses
                .PendingApproval,
            first.Status);
        Assert.Equal(1, first.ApprovalCount);

        var cfoService = CreateService(
            repository.Object,
            cfoId,
            Roles.CFO);

        var second = await cfoService.Approve(
            batch.Id,
            new ReviewHistoricalImportDto
            {
                ConcurrencyToken =
                    batch.ConcurrencyToken,
                Comment = "CFO approval"
            });

        Assert.Equal(
            HistoricalImportStatuses.Approved,
            second.Status);
        Assert.Equal(2, second.ApprovalCount);
        Assert.NotNull(second.ApprovedAtUtc);
    }

    [Fact]
    public async Task
        CommitHistorical_CreatesRecordsWithoutFinancialPosting()
    {
        var uploaderId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var account = CreateAccount(balance: 50_000m);
        var batch = CreateApprovedBatch(
            HistoricalImportModes
                .HistoricalTransactions,
            uploaderId,
            account,
            Roles.FinanceManager);

        var repository = CreateRepository(batch);
        repository
            .Setup(item =>
                item.GetAccountsForUpdate(
                    It.IsAny<
                        IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(
                new Dictionary<Guid, Account>
                {
                    [account.Id] = account
                });

        IReadOnlyCollection<
            HistoricalTransactionRecord>? records =
            null;
        repository
            .Setup(item =>
                item.AddHistoricalRecords(
                    It.IsAny<IReadOnlyCollection<
                        HistoricalTransactionRecord>>()))
            .Callback<IReadOnlyCollection<
                HistoricalTransactionRecord>>(
                items => records = items)
            .Returns(Task.CompletedTask);

        var service = CreateService(
            repository.Object,
            adminId,
            Roles.Admin);

        var result = await service.Commit(
            batch.Id,
            new HistoricalImportConcurrencyDto
            {
                ConcurrencyToken =
                    batch.ConcurrencyToken
            });

        Assert.Equal(1, result.HistoricalRecordCount);
        Assert.Equal(0, result.OpeningBalancePostingCount);
        Assert.Equal(
            HistoricalImportStatuses.Committed,
            result.Batch.Status);
        Assert.Single(records!);
        Assert.Equal(50_000m, account.Balance);

        repository.Verify(
            item => item.AddTreasuryTransactions(
                It.IsAny<IReadOnlyCollection<
                    TreasuryTransaction>>()),
            Times.Never);
        repository.Verify(
            item => item.AddLedgerEntries(
                It.IsAny<IReadOnlyCollection<
                    LedgerEntry>>()),
            Times.Never);
    }

    [Fact]
    public async Task
        CommitCutover_PostsBalanceTransactionAndLedgerAtomically()
    {
        var uploaderId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var account = CreateAccount(balance: 0);
        var batch = CreateApprovedBatch(
            HistoricalImportModes
                .CutoverOpeningBalances,
            uploaderId,
            account,
            Roles.Admin,
            Roles.CFO);
        batch.Rows.Single().Amount = 750_000m;
        batch.Rows.Single().Direction = null;
        batch.Rows.Single().TransactionType =
            "OpeningBalance";

        var repository = CreateRepository(batch);
        repository
            .Setup(item =>
                item.GetAccountsForUpdate(
                    It.IsAny<
                        IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(
                new Dictionary<Guid, Account>
                {
                    [account.Id] = account
                });
        repository
            .Setup(item =>
                item.GetAccountIdsWithFinancialActivity(
                    It.IsAny<
                        IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new HashSet<Guid>());

        IReadOnlyCollection<TreasuryTransaction>?
            transactions = null;
        IReadOnlyCollection<LedgerEntry>?
            ledgerEntries = null;

        repository
            .Setup(item =>
                item.AddTreasuryTransactions(
                    It.IsAny<IReadOnlyCollection<
                        TreasuryTransaction>>()))
            .Callback<IReadOnlyCollection<
                TreasuryTransaction>>(
                items => transactions = items)
            .Returns(Task.CompletedTask);
        repository
            .Setup(item =>
                item.AddLedgerEntries(
                    It.IsAny<IReadOnlyCollection<
                        LedgerEntry>>()))
            .Callback<IReadOnlyCollection<
                LedgerEntry>>(
                items => ledgerEntries = items)
            .Returns(Task.CompletedTask);

        var service = CreateService(
            repository.Object,
            adminId,
            Roles.Admin);

        var result = await service.Commit(
            batch.Id,
            new HistoricalImportConcurrencyDto
            {
                ConcurrencyToken =
                    batch.ConcurrencyToken
            });

        Assert.Equal(
            750_000m,
            account.Balance);
        Assert.Equal(1, result.OpeningBalancePostingCount);

        var transaction = Assert.Single(
            transactions!);
        Assert.Equal(
            TransactionTypes.OpeningBalance,
            transaction.TransactionType);
        Assert.Equal(
            account.Id,
            transaction.DestinationAccountId);
        Assert.Equal(
            TransactionStatuses.Completed,
            transaction.Status);

        var ledgerEntry = Assert.Single(
            ledgerEntries!);
        Assert.Equal("Debit", ledgerEntry.EntryType);
        Assert.Equal(750_000m, ledgerEntry.Amount);
        Assert.Equal(
            transaction.Id,
            ledgerEntry.TreasuryTransactionId);
    }

    private Mock<
        IHistoricalTransactionImportRepository>
        CreateRepository(
            HistoricalTransactionImportBatch batch)
    {
        var repository =
            new Mock<
                IHistoricalTransactionImportRepository>();

        repository
            .Setup(item =>
                item.GetBatchForUpdate(batch.Id))
            .ReturnsAsync(batch);
        repository
            .Setup(item =>
                item.GetBatch(batch.Id))
            .ReturnsAsync(batch);
        repository
            .Setup(item =>
                item.HasDecision(
                    batch.Id,
                    It.IsAny<Guid>()))
            .ReturnsAsync(
                (Guid _,
                 Guid userId) =>
                    batch.Decisions.Any(
                        decision =>
                            decision
                                .ApproverUserId ==
                            userId));
        repository
            .Setup(item =>
                item.AddDecision(
                    It.IsAny<
                        HistoricalTransactionImportDecision>()))
            .Callback<
                HistoricalTransactionImportDecision>(
                decision =>
                    batch.Decisions.Add(decision))
            .Returns(Task.CompletedTask);
        repository
            .Setup(item =>
                item.GetFingerprintsInValidatedBatches(
                    It.IsAny<string>(),
                    It.IsAny<
                        IReadOnlyCollection<string>>(),
                    It.IsAny<Guid?>()))
            .ReturnsAsync(new HashSet<string>());
        repository
            .Setup(item => item.BeginTransaction())
            .Returns(Task.CompletedTask);
        repository
            .Setup(item => item.CommitTransaction())
            .Returns(Task.CompletedTask);
        repository
            .Setup(item => item.RollbackTransaction())
            .Returns(Task.CompletedTask);
        repository
            .Setup(item => item.SaveChanges())
            .Returns(Task.CompletedTask);

        return repository;
    }

    private HistoricalTransactionImportService
        CreateService(
            IHistoricalTransactionImportRepository
                repository,
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
            .Returns(_organizationId);
        currentUser
            .SetupGet(item => item.Role)
            .Returns(role);

        var audit = new Mock<IAuditLogService>();
        audit
            .Setup(item =>
                item.Record(
                    It.IsAny<CreateAuditLogDto>()))
            .Returns(Task.CompletedTask);

        return new HistoricalTransactionImportService(
            repository,
            currentUser.Object,
            audit.Object,
            Options.Create(
                new HistoricalImportOptions()),
            TimeProvider.System);
    }

    private HistoricalTransactionImportBatch
        CreateApprovedBatch(
            string mode,
            Guid uploaderId,
            Account account,
            params string[] approverRoles)
    {
        var batch = CreateBatch(
            mode,
            HistoricalImportStatuses.Approved,
            uploaderId);
        batch.SubmittedByUserId = uploaderId;
        batch.SubmittedAtUtc = DateTime.UtcNow;
        batch.RequiredApprovalCount =
            approverRoles.Length;
        batch.ApprovalCount =
            approverRoles.Length;
        batch.ApprovedAtUtc = DateTime.UtcNow;

        foreach (var role in approverRoles)
        {
            batch.Decisions.Add(
                new HistoricalTransactionImportDecision
                {
                    Id = Guid.NewGuid(),
                    OrganizationId =
                        _organizationId,
                    BatchId = batch.Id,
                    ApproverUserId =
                        Guid.NewGuid(),
                    ApproverRole = role,
                    Decision =
                        ApprovalDecisionTypes.Approved
                });
        }

        batch.Rows.Add(
            new HistoricalTransactionImportRow
            {
                Id = Guid.NewGuid(),
                OrganizationId =
                    _organizationId,
                BatchId = batch.Id,
                RowNumber = 2,
                ExternalReference = "LEGACY-001",
                AccountNumber =
                    account.AccountNumber,
                AccountId = account.Id,
                LegalEntityId =
                    account.LegalEntityId,
                BusinessUnitId =
                    account.BusinessUnitId,
                TransactionDateUtc =
                    new DateTime(
                        2025,
                        1,
                        1,
                        0,
                        0,
                        0,
                        DateTimeKind.Utc),
                Amount = 500m,
                Currency = account.Currency,
                Direction =
                    HistoricalTransactionDirections
                        .Credit,
                TransactionType = "LegacyReceipt",
                Description = "Legacy receipt",
                Fingerprint =
                    new string('A', 64),
                IsValid = true
            });

        return batch;
    }

    private HistoricalTransactionImportBatch CreateBatch(
        string mode,
        string status,
        Guid uploaderId)
    {
        return new HistoricalTransactionImportBatch
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            ImportKey = Guid.NewGuid(),
            Mode = mode,
            Status = status,
            FileName = "history.csv",
            FileHash = new string('B', 64),
            TotalRowCount = 1,
            ValidRowCount = 1,
            InvalidRowCount = 0,
            UploadedByUserId = uploaderId,
            ConcurrencyToken = Guid.NewGuid()
        };
    }

    private Account CreateAccount(decimal balance)
    {
        return new Account
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            AccountNumber = "100001",
            Name = "Operating account",
            Currency = "NGN",
            Balance = balance,
            ReservedBalance = 0,
            IsActive = true,
            ConcurrencyToken = Guid.NewGuid()
        };
    }
}

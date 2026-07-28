using System.Text;
using Microsoft.Extensions.Options;
using Moq;
using Treasury.Application.DTOs.HistoricalImports;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Services;
using Treasury.Shared.Constants;

namespace Treasury.Tests.HistoricalImports;

public class HistoricalTransactionImportReportingTests
{
    private readonly Guid _organizationId =
        Guid.NewGuid();

    [Fact]
    public async Task
        SearchBatches_NormalizesFiltersAndPaginates()
    {
        var repository = CreateRepository();
        HistoricalImportBatchQueryDto? captured =
            null;
        var batch = CreateBatch(
            HistoricalImportModes
                .HistoricalTransactions,
            HistoricalImportStatuses
                .PendingApproval);

        repository
            .Setup(item =>
                item.SearchBatches(
                    It.IsAny<
                        HistoricalImportBatchQueryDto>()))
            .Callback<
                HistoricalImportBatchQueryDto>(
                query => captured = query)
            .ReturnsAsync((
                (IReadOnlyList<
                    HistoricalTransactionImportBatch>)
                    new[] { batch },
                1));

        var result = await CreateService(
                repository.Object)
            .SearchBatches(
                new HistoricalImportBatchQueryDto
                {
                    Mode = "historicaltransactions",
                    Status = "pendingapproval",
                    Search = "  history.csv  ",
                    Page = 0,
                    PageSize = 500
                });

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(200, result.PageSize);
        Assert.Equal(
            HistoricalImportModes
                .HistoricalTransactions,
            captured!.Mode);
        Assert.Equal(
            HistoricalImportStatuses
                .PendingApproval,
            captured.Status);
        Assert.Equal("history.csv", captured.Search);
    }

    [Fact]
    public async Task
        ExportCommittedRecords_NeutralizesSpreadsheetFormula()
    {
        var repository = CreateRepository();
        var account = CreateAccount(balance: 0);
        var record =
            new HistoricalTransactionRecord
            {
                Id = Guid.NewGuid(),
                OrganizationId = _organizationId,
                BatchId = Guid.NewGuid(),
                ExternalReference =
                    "=HYPERLINK(\"https://example.com\")",
                AccountId = account.Id,
                Account = account,
                TransactionDateUtc =
                    DateTime.SpecifyKind(
                        new DateTime(2025, 1, 1),
                        DateTimeKind.Utc),
                Amount = 500m,
                Currency = "NGN",
                Direction =
                    HistoricalTransactionDirections
                        .Credit,
                TransactionType = "LegacyReceipt",
                Description = "+formula",
                CommittedAtUtc = DateTime.UtcNow,
                CommittedByUserId = Guid.NewGuid()
            };

        repository
            .Setup(item =>
                item.GetCommittedRecords(
                    It.IsAny<
                        HistoricalTransactionRecordQueryDto>()))
            .ReturnsAsync((
                (IReadOnlyList<
                    HistoricalTransactionRecord>)
                    new[] { record },
                1));

        var export = await CreateService(
                repository.Object)
            .ExportCommittedRecords(
                new HistoricalTransactionRecordQueryDto(),
                5000);
        var csv =
            Encoding.UTF8.GetString(export.Content);

        Assert.Contains(
            "'=HYPERLINK",
            csv);
        Assert.Contains("'+formula", csv);
    }

    [Fact]
    public async Task
        ApprovalReport_CutoverShowsAdminAndCfoEvidence()
    {
        var repository = CreateRepository();
        var batch = CreateBatch(
            HistoricalImportModes
                .CutoverOpeningBalances,
            HistoricalImportStatuses.Approved);
        batch.RequiredApprovalCount = 2;
        batch.ApprovalCount = 2;

        batch.Decisions.Add(
            CreateDecision(
                batch,
                Roles.Admin));
        batch.Decisions.Add(
            CreateDecision(
                batch,
                Roles.CFO));

        repository
            .Setup(item =>
                item.GetBatchForReport(batch.Id))
            .ReturnsAsync(batch);

        var report = await CreateService(
                repository.Object)
            .GetApprovalReport(batch.Id);

        Assert.True(report.HasRequiredApprovals);
        Assert.True(report.HasAdminApproval);
        Assert.True(report.HasCfoApproval);
        Assert.False(
            report.HasFinanceManagerApproval);
        Assert.Equal(2, report.Decisions.Count);
    }

    [Fact]
    public async Task
        Reconciliation_SeparatesPostingEvidenceFromLaterBalanceDrift()
    {
        var repository = CreateRepository();
        var batch = CreateBatch(
            HistoricalImportModes
                .CutoverOpeningBalances,
            HistoricalImportStatuses.Committed);
        var account =
            CreateAccount(balance: 1_500m);
        var transactionId = Guid.NewGuid();
        var transaction =
            new TreasuryTransaction
            {
                Id = transactionId,
                OrganizationId = _organizationId,
                Reference = "TRX-OPEN-001",
                TransactionType =
                    TransactionTypes.OpeningBalance,
                Status =
                    TransactionStatuses.Completed,
                Amount = 1_000m,
                Currency = "NGN",
                DestinationAccountId = account.Id
            };
        transaction.LedgerEntries.Add(
            new LedgerEntry
            {
                Id = Guid.NewGuid(),
                OrganizationId = _organizationId,
                AccountId = account.Id,
                TreasuryTransactionId =
                    transactionId,
                Amount = 1_000m,
                EntryType = "Debit"
            });

        batch.Rows.Add(
            new HistoricalTransactionImportRow
            {
                Id = Guid.NewGuid(),
                OrganizationId = _organizationId,
                BatchId = batch.Id,
                RowNumber = 2,
                AccountId = account.Id,
                Account = account,
                AccountNumber =
                    account.AccountNumber,
                Amount = 1_000m,
                Currency = "NGN",
                IsValid = true,
                PostedTreasuryTransactionId =
                    transactionId,
                PostedTreasuryTransaction =
                    transaction
            });

        repository
            .Setup(item =>
                item.GetBatchForReport(batch.Id))
            .ReturnsAsync(batch);

        var report = await CreateService(
                repository.Object)
            .GetOpeningBalanceReconciliation(
                batch.Id);
        var row = Assert.Single(report.Rows);

        Assert.True(row.TransactionMatchesImport);
        Assert.True(row.LedgerMatchesImport);
        Assert.True(row.IsPostingReconciled);
        Assert.False(
            row.CurrentBalanceMatchesOpening);
        Assert.True(report.IsFullyPostingReconciled);
        Assert.Equal(1, report.CurrentBalanceDriftCount);
    }

    [Fact]
    public async Task Dashboard_ReturnsOperationalTotals()
    {
        var repository = CreateRepository();
        repository
            .Setup(item =>
                item.GetDashboardSummary())
            .ReturnsAsync(
                new HistoricalImportDashboardResponseDto
                {
                    TotalBatchCount = 8,
                    PendingApprovalCount = 2,
                    RejectedCount = 1,
                    CommittedCount = 4,
                    HistoricalTransactionRecordCount =
                        120,
                    OpeningBalancePostingCount = 10
                });

        var result = await CreateService(
                repository.Object)
            .GetDashboard();

        Assert.Equal(8, result.TotalBatchCount);
        Assert.Equal(2, result.PendingApprovalCount);
        Assert.Equal(
            120,
            result.HistoricalTransactionRecordCount);
        Assert.NotEqual(
            default,
            result.GeneratedAtUtc);
    }

    private Mock<
        IHistoricalTransactionImportRepository>
        CreateRepository()
    {
        return new Mock<
            IHistoricalTransactionImportRepository>();
    }

    private HistoricalTransactionImportService
        CreateService(
            IHistoricalTransactionImportRepository
                repository)
    {
        var currentUser =
            new Mock<ICurrentUserService>();
        currentUser
            .SetupGet(item => item.UserId)
            .Returns(Guid.NewGuid());
        currentUser
            .SetupGet(item => item.OrganizationId)
            .Returns(_organizationId);
        currentUser
            .SetupGet(item => item.Role)
            .Returns(Roles.Admin);

        return new HistoricalTransactionImportService(
            repository,
            currentUser.Object,
            Mock.Of<IAuditLogService>(),
            Options.Create(
                new HistoricalImportOptions()),
            TimeProvider.System);
    }

    private HistoricalTransactionImportBatch CreateBatch(
        string mode,
        string status)
    {
        return new HistoricalTransactionImportBatch
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            ImportKey = Guid.NewGuid(),
            Mode = mode,
            Status = status,
            FileName = "history.csv",
            FileHash = new string('A', 64),
            TotalRowCount = 1,
            ValidRowCount = 1,
            UploadedByUserId = Guid.NewGuid(),
            UploadedAtUtc = DateTime.UtcNow,
            ValidatedAtUtc = DateTime.UtcNow,
            SubmittedByUserId = Guid.NewGuid(),
            SubmittedAtUtc = DateTime.UtcNow,
            ApprovedAtUtc =
                status is
                    HistoricalImportStatuses.Approved or
                    HistoricalImportStatuses.Committed
                    ? DateTime.UtcNow
                    : null,
            CommittedByUserId =
                status ==
                    HistoricalImportStatuses.Committed
                    ? Guid.NewGuid()
                    : null,
            CommittedAtUtc =
                status ==
                    HistoricalImportStatuses.Committed
                    ? DateTime.UtcNow
                    : null
        };
    }

    private HistoricalTransactionImportDecision
        CreateDecision(
            HistoricalTransactionImportBatch batch,
            string role)
    {
        return new HistoricalTransactionImportDecision
        {
            Id = Guid.NewGuid(),
            OrganizationId = _organizationId,
            BatchId = batch.Id,
            ApproverUserId = Guid.NewGuid(),
            ApproverRole = role,
            Decision =
                ApprovalDecisionTypes.Approved,
            CreatedAtUtc = DateTime.UtcNow
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
            IsActive = true
        };
    }
}

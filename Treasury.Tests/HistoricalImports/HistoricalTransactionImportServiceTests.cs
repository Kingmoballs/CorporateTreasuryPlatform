using System.Text;
using Microsoft.Extensions.Options;
using Moq;
using Treasury.Application.DTOs.Audit;
using Treasury.Application.DTOs.HistoricalImports;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Services;
using Treasury.Shared.Constants;

namespace Treasury.Tests.HistoricalImports;

public class HistoricalTransactionImportServiceTests
{
    private readonly Guid _organizationId =
        Guid.NewGuid();

    private readonly Guid _userId =
        Guid.NewGuid();

    [Fact]
    public async Task DryRun_ValidHistoricalCsv_StagesWithoutPosting()
    {
        var account = CreateAccount();
        HistoricalTransactionImportBatch? savedBatch =
            null;

        var repository = CreateRepository(account);
        repository
            .Setup(item => item.Add(
                It.IsAny<
                    HistoricalTransactionImportBatch>()))
            .Callback<
                HistoricalTransactionImportBatch>(
                batch => savedBatch = batch)
            .Returns(Task.CompletedTask);

        var audit = CreateAuditService();
        var service = CreateService(
            repository.Object,
            audit.Object);

        var result = await service.DryRun(
            CreateHistoricalRequest(
                "LEG-001,100001,,,2025-01-15T09:30:00Z," +
                "2025-01-15T09:30:00Z,1250.50,NGN,Credit," +
                "CustomerReceipt,Legacy receipt,Receipts," +
                "Example Customer"));

        Assert.Equal(
            HistoricalImportStatuses.Validated,
            result.Status);
        Assert.Equal(1, result.ValidRowCount);
        Assert.Equal(0, result.InvalidRowCount);
        Assert.False(result.IsPostingOperation);

        Assert.NotNull(savedBatch);
        Assert.Single(savedBatch.Rows);
        Assert.True(savedBatch.Rows.Single().IsValid);
        Assert.Equal(
            0m,
            account.Balance);
        Assert.Equal(
            0m,
            account.ReservedBalance);

        audit.Verify(
            item => item.Record(
                It.Is<CreateAuditLogDto>(entry =>
                    entry.EntityType ==
                    AuditEntityTypes
                        .HistoricalTransactionImportBatch)),
            Times.Once);
    }

    [Fact]
    public async Task DryRun_DimensionMismatch_StagesRowLevelError()
    {
        var account = CreateAccount(
            withDimensions: true);
        HistoricalTransactionImportBatch? savedBatch =
            null;

        var repository = CreateRepository(account);
        repository
            .Setup(item => item.Add(
                It.IsAny<
                    HistoricalTransactionImportBatch>()))
            .Callback<
                HistoricalTransactionImportBatch>(
                batch => savedBatch = batch)
            .Returns(Task.CompletedTask);

        var service = CreateService(
            repository.Object,
            CreateAuditService().Object);

        var result = await service.DryRun(
            CreateHistoricalRequest(
                "LEG-002,100001,WRONG,WRONG," +
                "2025-01-15T09:30:00Z,,50,NGN,Debit," +
                "BankCharge,Legacy fee,Fees,Bank"));

        Assert.Equal(
            HistoricalImportStatuses
                .ValidationFailed,
            result.Status);
        Assert.Equal(1, result.InvalidRowCount);

        var row = Assert.Single(savedBatch!.Rows);
        Assert.False(row.IsValid);
        Assert.Contains(
            "does not match",
            row.ValidationErrorsJson);
    }

    [Fact]
    public async Task DryRun_RepeatedKeyAndFile_ReturnsIdempotentReplay()
    {
        var importKey = Guid.NewGuid();
        var content = CreateHistoricalContent(
            "LEG-003,100001,,,2025-01-15T09:30:00Z," +
            ",50,NGN,Credit,Receipt,Legacy receipt,," +
            "Customer");
        var hash = Convert.ToHexString(
            System.Security.Cryptography.SHA256
                .HashData(content));

        var repository =
            new Mock<
                IHistoricalTransactionImportRepository>();

        repository
            .Setup(item =>
                item.GetByImportKey(importKey))
            .ReturnsAsync(
                new HistoricalTransactionImportBatch
                {
                    Id = Guid.NewGuid(),
                    OrganizationId =
                        _organizationId,
                    ImportKey = importKey,
                    Mode =
                        HistoricalImportModes
                            .HistoricalTransactions,
                    Status =
                        HistoricalImportStatuses
                            .Validated,
                    FileName = "history.csv",
                    FileHash = hash,
                    TotalRowCount = 1,
                    ValidRowCount = 1,
                    UploadedByUserId = _userId
                });

        var service = CreateService(
            repository.Object,
            CreateAuditService().Object);

        var result = await service.DryRun(
            new CreateHistoricalImportDryRunDto
            {
                ImportKey = importKey,
                Mode =
                    HistoricalImportModes
                        .HistoricalTransactions,
                FileName = "history.csv",
                FileContent = content
            });

        Assert.True(result.IsIdempotentReplay);
        repository.Verify(
            item => item.Add(
                It.IsAny<
                    HistoricalTransactionImportBatch>()),
            Times.Never);
    }

    [Fact]
    public async Task DryRun_CutoverWithActivity_IsRejectedWithoutPosting()
    {
        var account = CreateAccount();
        HistoricalTransactionImportBatch? savedBatch =
            null;

        var repository = CreateRepository(account);
        repository
            .Setup(item =>
                item.GetAccountIdsWithFinancialActivity(
                    It.IsAny<
                        IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(
                new HashSet<Guid>
                {
                    account.Id
                });
        repository
            .Setup(item => item.Add(
                It.IsAny<
                    HistoricalTransactionImportBatch>()))
            .Callback<
                HistoricalTransactionImportBatch>(
                batch => savedBatch = batch)
            .Returns(Task.CompletedTask);

        var service = CreateService(
            repository.Object,
            CreateAuditService().Object);

        var csv =
            "ExternalReference,AccountNumber," +
            "LegalEntityCode,BusinessUnitCode," +
            "CutoverDateUtc,OpeningBalance,Currency," +
            "Description\r\n" +
            "OPEN-001,100001,,,2025-01-01,5000,NGN," +
            "Opening cash";

        var result = await service.DryRun(
            new CreateHistoricalImportDryRunDto
            {
                ImportKey = Guid.NewGuid(),
                Mode =
                    HistoricalImportModes
                        .CutoverOpeningBalances,
                FileName = "opening.csv",
                FileContent =
                    Encoding.UTF8.GetBytes(csv)
            });

        Assert.Equal(
            HistoricalImportStatuses
                .ValidationFailed,
            result.Status);
        Assert.Contains(
            "no ledger entries",
            Assert.Single(savedBatch!.Rows)
                .ValidationErrorsJson);
        Assert.Equal(0m, account.Balance);
    }

    [Fact]
    public async Task DryRun_OverlongValues_AreStagedAsErrors()
    {
        var account = CreateAccount();
        HistoricalTransactionImportBatch? savedBatch =
            null;

        var repository = CreateRepository(account);
        repository
            .Setup(item => item.Add(
                It.IsAny<
                    HistoricalTransactionImportBatch>()))
            .Callback<
                HistoricalTransactionImportBatch>(
                batch => savedBatch = batch)
            .Returns(Task.CompletedTask);

        var service = CreateService(
            repository.Object,
            CreateAuditService().Object);

        var result = await service.DryRun(
            CreateHistoricalRequest(
                $"{new string('R', 101)},100001,,,2025-01-15," +
                $",50,NGNN,Credit,{new string('T', 101)}," +
                $"{new string('D', 501)},,Customer"));

        Assert.Equal(
            HistoricalImportStatuses
                .ValidationFailed,
            result.Status);

        var row = Assert.Single(savedBatch!.Rows);
        Assert.False(row.IsValid);
        Assert.Equal(
            100,
            row.ExternalReference!.Length);
        Assert.Equal(3, row.Currency!.Length);
        Assert.Equal(
            100,
            row.TransactionType!.Length);
        Assert.Equal(
            500,
            row.Description!.Length);
    }

    [Fact]
    public async Task
        DryRun_TwoOpeningRowsForAccount_AreBothDuplicates()
    {
        var account = CreateAccount();
        HistoricalTransactionImportBatch? savedBatch =
            null;

        var repository = CreateRepository(account);
        repository
            .Setup(item => item.Add(
                It.IsAny<
                    HistoricalTransactionImportBatch>()))
            .Callback<
                HistoricalTransactionImportBatch>(
                batch => savedBatch = batch)
            .Returns(Task.CompletedTask);

        var service = CreateService(
            repository.Object,
            CreateAuditService().Object);

        var csv =
            "ExternalReference,AccountNumber," +
            "LegalEntityCode,BusinessUnitCode," +
            "CutoverDateUtc,OpeningBalance,Currency," +
            "Description\r\n" +
            "OPEN-001,100001,,,2025-01-01,5000,NGN," +
            "Opening cash\r\n" +
            "OPEN-002,100001,,,2025-01-02,5000,NGN," +
            "Duplicate opening cash";

        var result = await service.DryRun(
            new CreateHistoricalImportDryRunDto
            {
                ImportKey = Guid.NewGuid(),
                Mode =
                    HistoricalImportModes
                        .CutoverOpeningBalances,
                FileName = "opening.csv",
                FileContent =
                    Encoding.UTF8.GetBytes(csv)
            });

        Assert.Equal(2, result.InvalidRowCount);
        Assert.All(
            savedBatch!.Rows,
            row => Assert.Contains(
                "duplicates another row",
                row.ValidationErrorsJson));
    }

    [Fact]
    public void GetTemplate_UsesModeSpecificSchemas()
    {
        var service = CreateService(
            new Mock<
                IHistoricalTransactionImportRepository>()
                .Object,
            CreateAuditService().Object);

        var historical = Encoding.UTF8.GetString(
            service.GetTemplate(
                    HistoricalImportModes
                        .HistoricalTransactions)
                .Content);

        var opening = Encoding.UTF8.GetString(
            service.GetTemplate(
                    HistoricalImportModes
                        .CutoverOpeningBalances)
                .Content);

        Assert.Contains(
            "TransactionDateUtc",
            historical);
        Assert.Contains("Direction", historical);
        Assert.DoesNotContain(
            "OpeningBalance",
            historical);

        Assert.Contains(
            "CutoverDateUtc",
            opening);
        Assert.Contains(
            "OpeningBalance",
            opening);
        Assert.DoesNotContain(
            "Direction",
            opening);
    }

    private Mock<
        IHistoricalTransactionImportRepository>
        CreateRepository(Account account)
    {
        var repository =
            new Mock<
                IHistoricalTransactionImportRepository>();

        repository
            .Setup(item =>
                item.GetByImportKey(
                    It.IsAny<Guid>()))
            .ReturnsAsync(
                (HistoricalTransactionImportBatch?)
                    null);
        repository
            .Setup(item =>
                item.GetByFileHash(
                    It.IsAny<string>(),
                    It.IsAny<string>()))
            .ReturnsAsync(
                (HistoricalTransactionImportBatch?)
                    null);
        repository
            .Setup(item =>
                item.GetAccountsByNumbers(
                    It.IsAny<
                        IReadOnlyCollection<string>>()))
            .ReturnsAsync(
                new Dictionary<string, Account>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    [account.AccountNumber] = account
                });
        repository
            .Setup(item =>
                item.GetFingerprintsInValidatedBatches(
                    It.IsAny<string>(),
                    It.IsAny<
                        IReadOnlyCollection<string>>()))
            .ReturnsAsync(
                new HashSet<string>());
        repository
            .Setup(item =>
                item.GetAccountIdsWithFinancialActivity(
                    It.IsAny<
                        IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(
                new HashSet<Guid>());
        repository
            .Setup(item => item.SaveChanges())
            .Returns(Task.CompletedTask);

        return repository;
    }

    private Mock<IAuditLogService>
        CreateAuditService()
    {
        var audit = new Mock<IAuditLogService>();
        audit
            .Setup(item =>
                item.Record(
                    It.IsAny<CreateAuditLogDto>()))
            .Returns(Task.CompletedTask);
        return audit;
    }

    private HistoricalTransactionImportService
        CreateService(
            IHistoricalTransactionImportRepository
                repository,
            IAuditLogService audit)
    {
        var currentUser =
            new Mock<ICurrentUserService>();
        currentUser
            .SetupGet(item => item.UserId)
            .Returns(_userId);
        currentUser
            .SetupGet(item => item.OrganizationId)
            .Returns(_organizationId);

        return new HistoricalTransactionImportService(
            repository,
            currentUser.Object,
            audit,
            Options.Create(
                new HistoricalImportOptions()),
            TimeProvider.System);
    }

    private CreateHistoricalImportDryRunDto
        CreateHistoricalRequest(string dataRow)
    {
        return new CreateHistoricalImportDryRunDto
        {
            ImportKey = Guid.NewGuid(),
            Mode =
                HistoricalImportModes
                    .HistoricalTransactions,
            FileName = "history.csv",
            FileContent =
                CreateHistoricalContent(dataRow)
        };
    }

    private static byte[] CreateHistoricalContent(
        string dataRow)
    {
        var csv =
            "ExternalReference,AccountNumber," +
            "LegalEntityCode,BusinessUnitCode," +
            "TransactionDateUtc,ValueDateUtc,Amount," +
            "Currency,Direction,TransactionType," +
            "Description,Category,CounterpartyName\r\n" +
            dataRow;

        return Encoding.UTF8.GetBytes(csv);
    }

    private Account CreateAccount(
        bool withDimensions = false)
    {
        var account =
            new Account
            {
                Id = Guid.NewGuid(),
                OrganizationId =
                    _organizationId,
                AccountNumber = "100001",
                Name = "Operating account",
                Currency = "NGN",
                IsActive = true,
                Balance = 0,
                ReservedBalance = 0
            };

        if (!withDimensions)
        {
            return account;
        }

        var legalEntity =
            new LegalEntity
            {
                Id = Guid.NewGuid(),
                OrganizationId =
                    _organizationId,
                Code = "LE-001",
                Name = "Default legal entity"
            };

        var businessUnit =
            new BusinessUnit
            {
                Id = Guid.NewGuid(),
                OrganizationId =
                    _organizationId,
                LegalEntityId =
                    legalEntity.Id,
                LegalEntity = legalEntity,
                Code = "BU-001",
                Name = "Default business unit"
            };

        account.LegalEntityId = legalEntity.Id;
        account.LegalEntity = legalEntity;
        account.BusinessUnitId = businessUnit.Id;
        account.BusinessUnit = businessUnit;

        return account;
    }
}

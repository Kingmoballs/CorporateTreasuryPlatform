using System.Text;
using Moq;
using Treasury.Application.DTOs.BankStatements;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Services;
using Treasury.Shared.Constants;

namespace Treasury.Tests.BankStatements;

public class BankStatementOrganizationDimensionTests
{
    [Fact]
    public async Task
        UnmatchedLinesAndExceptionExport_PreserveAccountScope()
    {
        var legalEntityId = Guid.NewGuid();
        var businessUnitId = Guid.NewGuid();
        var account =
            new Account
            {
                Id = Guid.NewGuid(),
                LegalEntityId = legalEntityId,
                BusinessUnitId = businessUnitId,
                Name = "Scoped operating account",
                AccountNumber = "SCOPED-001",
                Currency = "NGN"
            };
        var statementImport =
            new BankStatementImport
            {
                Id = Guid.NewGuid(),
                AccountId = account.Id,
                Account = account,
                FileName = "scoped-statement.csv",
                Currency = "NGN",
                LineCount = 1,
                UploadedAtUtc = DateTime.UtcNow
            };
        var line =
            new BankStatementLine
            {
                Id = Guid.NewGuid(),
                BankStatementImportId =
                    statementImport.Id,
                BankStatementImport =
                    statementImport,
                AccountId = account.Id,
                Account = account,
                LineNumber = 1,
                TransactionDateUtc =
                    DateTime.UtcNow.AddDays(-1),
                Description = "Scoped receipt",
                Amount = 100m,
                Currency = "NGN",
                ReconciliationStatus =
                    ReconciliationStatus.Unmatched
            };

        statementImport.Lines.Add(line);

        var bankStatementRepository =
            new Mock<IBankStatementRepository>();

        bankStatementRepository
            .Setup(item =>
                item.GetUnmatchedLines(
                    null,
                    null,
                    null,
                    legalEntityId,
                    businessUnitId))
            .ReturnsAsync(
                new List<BankStatementLine>
                {
                    line
                });
        bankStatementRepository
            .Setup(item =>
                item.GetImportById(
                    statementImport.Id))
            .ReturnsAsync(statementImport);

        var service =
            CreateService(
                bankStatementRepository.Object);
        var unmatchedLines =
            await service.GetUnmatchedLines(
                new UnmatchedBankStatementLinesQueryDto
                {
                    LegalEntityId = legalEntityId,
                    BusinessUnitId = businessUnitId
                });

        var unmatchedLine =
            Assert.Single(unmatchedLines);

        Assert.Equal(
            legalEntityId,
            unmatchedLine.LegalEntityId);
        Assert.Equal(
            businessUnitId,
            unmatchedLine.BusinessUnitId);

        var report =
            await service.GetExceptionReport(
                statementImport.Id);
        var export =
            await service.ExportExceptionReportCsv(
                statementImport.Id);
        var csv =
            Encoding.UTF8.GetString(export.Content);

        Assert.Equal(
            legalEntityId,
            report.LegalEntityId);
        Assert.Equal(
            businessUnitId,
            report.BusinessUnitId);
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

    [Fact]
    public async Task
        UnmatchedLines_EmptyOrganizationDimensionIsRejected()
    {
        var bankStatementRepository =
            new Mock<IBankStatementRepository>();
        var service =
            CreateService(
                bankStatementRepository.Object);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.GetUnmatchedLines(
                new UnmatchedBankStatementLinesQueryDto
                {
                    LegalEntityId = Guid.Empty
                }));

        bankStatementRepository.Verify(
            item =>
                item.GetUnmatchedLines(
                    It.IsAny<Guid?>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<Guid?>()),
            Times.Never);
    }

    private static BankStatementService CreateService(
        IBankStatementRepository bankStatementRepository)
    {
        return new BankStatementService(
            bankStatementRepository,
            new Mock<IAccountRepository>().Object,
            new Mock<ICurrentUserService>().Object,
            new Mock<
                ITreasuryTransactionRepository>().Object,
            new Mock<IAuditLogService>().Object);
    }
}

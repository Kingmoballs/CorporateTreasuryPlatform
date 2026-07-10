using Microsoft.EntityFrameworkCore;
using Moq;
using Treasury.Application.DTOs.BankStatements;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;
using Treasury.Infrastructure.Repositories;
using Treasury.Infrastructure.Services;
using Treasury.Shared.Constants;

namespace Treasury.Tests.Integration;

public class BankStatementReconciliationIntegrationTests
{
    [Fact]
    public async Task AutoMatchAndReconcile_UpdatesReportsCorrectly()
    {
        // Arrange
        await using var database =
            await PostgreSqlTestDatabase.Start();

        var seeded =
            await SeedRequiredData(database);

        Guid receiptTransactionId;

        await using (
            var setupContext =
                database.CreateContext())
        {
            var transaction =
                CreateCompletedTransaction(
                    seeded.AccountId,
                    amount: 2_500_000m,
                    signedAmountIsInflow: true,
                    referencePrefix: "BS-REC");

            await setupContext.TreasuryTransactions
                .AddAsync(transaction);

            await setupContext.SaveChangesAsync();

            receiptTransactionId =
                transaction.Id;
        }

        Guid importId;
        Guid lineId;

        await using (
            var importContext =
                database.CreateContext())
        {
            var service =
                CreateBankStatementService(
                    importContext,
                    seeded.UserId);

            var import =
                await service.ImportStatement(
                    new CreateBankStatementImportDto
                    {
                        AccountId =
                            seeded.AccountId,

                        FileName =
                            "gtbank-july-statement.csv",

                        StatementReference =
                            $"AUTO-{Guid.NewGuid():N}",

                        Currency =
                            "NGN",

                        StatementFromUtc =
                            new DateTime(
                                2026,
                                7,
                                1,
                                0,
                                0,
                                0,
                                DateTimeKind.Utc),

                        StatementToUtc =
                            new DateTime(
                                2026,
                                7,
                                31,
                                23,
                                59,
                                59,
                                DateTimeKind.Utc),

                        OpeningBalance =
                            50_000_000m,

                        ClosingBalance =
                            52_500_000m,

                        Lines =
                        [
                            new CreateBankStatementLineDto
                            {
                                LineNumber =
                                    1,

                                TransactionDateUtc =
                                    DateTime.UtcNow,

                                ValueDateUtc =
                                    DateTime.UtcNow,

                                Description =
                                    "Customer receipt from ABC Limited",

                                BankReference =
                                    "BNK-REC-001",

                                CounterpartyName =
                                    "ABC Limited",

                                Amount =
                                    2_500_000m,

                                Currency =
                                    "NGN",

                                BalanceAfterTransaction =
                                    52_500_000m
                            }
                        ]
                    });

            importId =
                import.Id;

            lineId =
                import.Lines.Single().Id;
        }

        BankStatementReconciliationResultDto matchResult;

        await using (
            var matchContext =
                database.CreateContext())
        {
            var service =
                CreateBankStatementService(
                    matchContext,
                    seeded.UserId);

            matchResult =
                await service.AutoMatchImport(
                    importId,
                    dateToleranceDays: 2);
        }

        // Assert auto-match
        Assert.Equal(
            1,
            matchResult.CandidateLineCount);

        Assert.Equal(
            1,
            matchResult.MatchedLineCount);

        Assert.Equal(
            0,
            matchResult.UnmatchedLineCount);

        Assert.Contains(
            lineId,
            matchResult.MatchedLineIds);

        await using (
            var reconcileContext =
                database.CreateContext())
        {
            var service =
                CreateBankStatementService(
                    reconcileContext,
                    seeded.UserId);

            var reconciledLine =
                await service.ReconcileLine(lineId);

            Assert.Equal(
                ReconciliationStatus.Reconciled,
                reconciledLine.ReconciliationStatus);

            Assert.Equal(
                receiptTransactionId,
                reconciledLine.MatchedTreasuryTransactionId);

            Assert.Equal(
                seeded.UserId,
                reconciledLine.ReconciledByUserId);
        }

        await using (
            var reportContext =
                database.CreateContext())
        {
            var service =
                CreateBankStatementService(
                    reportContext,
                    seeded.UserId);

            var summary =
                await service.GetReconciliationSummary(
                    importId);

            var exceptions =
                await service.GetExceptionReport(
                    importId);

            Assert.Equal(
                1,
                summary.TotalLineCount);

            Assert.Equal(
                0,
                summary.UnmatchedLineCount);

            Assert.Equal(
                0,
                summary.MatchedLineCount);

            Assert.Equal(
                1,
                summary.ReconciledLineCount);

            Assert.Equal(
                0,
                summary.ActionRequiredLineCount);

            Assert.Equal(
                100m,
                summary.ReconciliationCompletionPercentage);

            Assert.Equal(
                0,
                exceptions.ActionRequiredLineCount);

            Assert.Empty(
                exceptions.Lines);
        }
    }

    [Fact]
    public async Task BookSideExceptionReport_ReturnsUnmatchedTreasuryTransactions()
    {
        // Arrange
        await using var database =
            await PostgreSqlTestDatabase.Start();

        var seeded =
            await SeedRequiredData(database);

        Guid unmatchedTransactionId;

        await using (
            var setupContext =
                database.CreateContext())
        {
            var transactionC =
                CreateCompletedTransaction(
                    seeded.AccountId,
                    amount: 4_000_000m,
                    signedAmountIsInflow: false,
                    referencePrefix: "BS-PAY");

            await setupContext.TreasuryTransactions
                .AddAsync(transactionC);

            await setupContext.SaveChangesAsync();

            unmatchedTransactionId =
                transactionC.Id;
        }

        Guid importId;

        await using (
            var importContext =
                database.CreateContext())
        {
            var service =
                CreateBankStatementService(
                    importContext,
                    seeded.UserId);

            var import =
                await service.ImportStatement(
                    new CreateBankStatementImportDto
                    {
                        AccountId =
                            seeded.AccountId,

                        FileName =
                            "access-july-statement.csv",

                        StatementReference =
                            $"BOOK-{Guid.NewGuid():N}",

                        Currency =
                            "NGN",

                        StatementFromUtc =
                            DateTime.UtcNow.AddDays(-1),

                        StatementToUtc =
                            DateTime.UtcNow.AddDays(1),

                        OpeningBalance =
                            50_000_000m,

                        ClosingBalance =
                            50_000_000m,

                        Lines =
                        [
                            new CreateBankStatementLineDto
                            {
                                LineNumber =
                                    1,

                                TransactionDateUtc =
                                    DateTime.UtcNow,

                                Description =
                                    "Bank charge not in treasury system",

                                BankReference =
                                    "BNK-ONLY-001",

                                CounterpartyName =
                                    "Bank",

                                Amount =
                                    -1_000m,

                                Currency =
                                    "NGN",

                                BalanceAfterTransaction =
                                    49_999_000m
                            }
                        ]
                    });

            importId =
                import.Id;
        }

        // Act
        BookSideExceptionReportDto report;

        await using (
            var reportContext =
                database.CreateContext())
        {
            var service =
                CreateBankStatementService(
                    reportContext,
                    seeded.UserId);

            report =
                await service.GetBookSideExceptionReport(
                    importId);
        }

        // Assert
        var transaction =
            Assert.Single(report.Transactions);

        Assert.Equal(
            1,
            report.UnmatchedTransactionCount);

        Assert.Equal(
            unmatchedTransactionId,
            transaction.Id);

        Assert.Equal(
            "Outflow",
            transaction.CashDirection);

        Assert.Equal(
            -4_000_000m,
            transaction.SignedAmount);

        Assert.Equal(
            4_000_000m,
            report.TotalUnmatchedOutflowAmount);
    }

    [Fact]
    public async Task ImportStatementFromCsv_ParsesAndStoresLines()
    {
        // Arrange
        await using var database =
            await PostgreSqlTestDatabase.Start();

        var seeded =
            await SeedRequiredData(database);

        const string csvContent =
            """
            LineNumber,TransactionDateUtc,ValueDateUtc,Description,BankReference,CounterpartyName,Amount,Currency,BalanceAfterTransaction
            1,2026-07-02T09:30:00Z,2026-07-02T09:30:00Z,Customer receipt from ABC Limited,BNK-CSV-001,ABC Limited,2500000,NGN,52500000
            2,2026-07-03T11:15:00Z,2026-07-03T11:15:00Z,Supplier payment to XYZ Services,BNK-CSV-002,XYZ Services,-4000000,NGN,48500000
            """;

        BankStatementImportResponseDto import;

        // Act
        await using (
            var importContext =
                database.CreateContext())
        {
            var service =
                CreateBankStatementService(
                    importContext,
                    seeded.UserId);

            import =
                await service.ImportStatementFromCsv(
                    new CreateBankStatementCsvImportDto
                    {
                        AccountId =
                            seeded.AccountId,

                        FileName =
                            "statement-upload.csv",

                        CsvContent =
                            csvContent,

                        StatementReference =
                            $"CSV-{Guid.NewGuid():N}",

                        Currency =
                            "NGN",

                        StatementFromUtc =
                            new DateTime(
                                2026,
                                7,
                                1,
                                0,
                                0,
                                0,
                                DateTimeKind.Utc),

                        StatementToUtc =
                            new DateTime(
                                2026,
                                7,
                                31,
                                23,
                                59,
                                59,
                                DateTimeKind.Utc),

                        OpeningBalance =
                            50_000_000m,

                        ClosingBalance =
                            48_500_000m
                    });
        }

        // Assert
        Assert.Equal(
            2,
            import.LineCount);

        Assert.Equal(
            2,
            import.Lines.Count);

        Assert.Contains(
            import.Lines,
            line =>
                line.Amount == 2_500_000m &&
                line.ReconciliationStatus ==
                    ReconciliationStatus.Unmatched);

        Assert.Contains(
            import.Lines,
            line =>
                line.Amount == -4_000_000m &&
                line.ReconciliationStatus ==
                    ReconciliationStatus.Unmatched);
    }

    private static BankStatementService CreateBankStatementService(
        TreasuryDbContext context,
        Guid userId)
    {
        return new BankStatementService(
            new BankStatementRepository(context),
            new AccountRepository(context),
            CreateCurrentUser(userId),
            new TreasuryTransactionRepository(context),
            new AuditLogService(
                new AuditLogRepository(context),
                CreateCurrentUser(userId)));
    }

    private static ICurrentUserService CreateCurrentUser(
        Guid userId)
    {
        var currentUser =
            new Mock<ICurrentUserService>();

        currentUser
            .SetupGet(service =>
                service.UserId)
            .Returns(userId);

        currentUser
            .SetupGet(service =>
                service.Email)
            .Returns("bankstatement-test@example.com");

        currentUser
            .SetupGet(service =>
                service.Role)
            .Returns(Roles.TreasuryOfficer);

        return currentUser.Object;
    }

    private static TreasuryTransaction CreateCompletedTransaction(
        Guid accountId,
        decimal amount,
        bool signedAmountIsInflow,
        string referencePrefix)
    {
        return new TreasuryTransaction
        {
            Id =
                Guid.NewGuid(),

            Reference =
                $"{referencePrefix}-{Guid.NewGuid():N}",

            TransactionType =
                signedAmountIsInflow
                    ? TransactionTypes.CashReceipt
                    : TransactionTypes.CashPayment,

            Status =
                TransactionStatuses.Completed,

            Amount =
                amount,

            Currency =
                "NGN",

            Description =
                signedAmountIsInflow
                    ? "Receipt for bank statement test"
                    : "Payment for bank statement test",

            SourceAccountId =
                signedAmountIsInflow
                    ? null
                    : accountId,

            DestinationAccountId =
                signedAmountIsInflow
                    ? accountId
                    : null,

            Category =
                signedAmountIsInflow
                    ? "CustomerReceipt"
                    : "SupplierPayment",

            CounterpartyName =
                signedAmountIsInflow
                    ? "ABC Limited"
                    : "XYZ Services",

            ExternalReference =
                $"{referencePrefix}-EXT-{Guid.NewGuid():N}",

            IdempotencyKey =
                $"{referencePrefix}-IDEMP-{Guid.NewGuid():N}",

            CreatedAtUtc =
                DateTime.UtcNow,

            CompletedAtUtc =
                DateTime.UtcNow
        };
    }

    private static async Task<SeededData> SeedRequiredData(
        PostgreSqlTestDatabase database)
    {
        await using var context =
            database.CreateContext();

        var role =
            new Role
            {
                Id =
                    Guid.NewGuid(),

                Name =
                    Roles.TreasuryOfficer
            };

        var user =
            new User
            {
                Id =
                    Guid.NewGuid(),

                FirstName =
                    "Bank",

                LastName =
                    "Statement",

                Email =
                    $"bankstatement-{Guid.NewGuid():N}@example.com",

                PasswordHash =
                    "not-used",

                RoleId =
                    role.Id,

                Role =
                    role,

                IsActive =
                    true,

                CreatedAt =
                    DateTime.UtcNow
            };

        var accountType =
            new AccountType
            {
                Id =
                    Guid.NewGuid(),

                Name =
                    AccountTypes.Operating
            };

        var account =
            new Account
            {
                Id =
                    Guid.NewGuid(),

                Name =
                    "Bank Statement Test Account",

                AccountNumber =
                    $"BS-{Guid.NewGuid():N}",

                Balance =
                    50_000_000m,

                ReservedBalance =
                    0m,

                Currency =
                    "NGN",

                IsActive =
                    true,

                AccountTypeId =
                    accountType.Id,

                AccountType =
                    accountType,

                ConcurrencyToken =
                    Guid.NewGuid(),

                CreatedAt =
                    DateTime.UtcNow
            };

        await context.Roles.AddAsync(
            role);

        await context.Users.AddAsync(
            user);

        await context.AccountTypes.AddAsync(
            accountType);

        await context.Accounts.AddAsync(
            account);

        await context.SaveChangesAsync();

        return new SeededData(
            user.Id,
            account.Id);
    }

    private sealed record SeededData(
        Guid UserId,
        Guid AccountId);
}
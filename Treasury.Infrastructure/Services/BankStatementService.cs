using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using Treasury.Application.Common.Exceptions;
using Treasury.Application.DTOs.Audit;
using Treasury.Application.DTOs.BankStatements;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Services;

public class BankStatementService
    : IBankStatementService
{
    private const int MaximumLineCount = 5000;

    private readonly IBankStatementRepository
        _bankStatementRepository;

    private readonly IAccountRepository
        _accountRepository;

    private readonly ICurrentUserService
        _currentUserService;
    
    private readonly ITreasuryTransactionRepository
        _transactionRepository;

    private readonly IAuditLogService
        _auditLogService;

    public BankStatementService(
        IBankStatementRepository bankStatementRepository,
        IAccountRepository accountRepository,
        ICurrentUserService currentUserService,
        ITreasuryTransactionRepository transactionRepository,
        IAuditLogService auditLogService)
    {
        _bankStatementRepository =
            bankStatementRepository;

        _accountRepository =
            accountRepository;

        _currentUserService =
            currentUserService;
        
        _transactionRepository =
            transactionRepository;
        
        _auditLogService =
            auditLogService;
    }

    public async Task<BankStatementImportResponseDto>
        ImportStatement(
            CreateBankStatementImportDto dto)
    {
        ValidateImportDto(dto);

        var account =
            await _accountRepository.GetById(
                dto.AccountId);

        if (account is null)
        {
            throw new ResourceNotFoundException(
                "Account not found.");
        }

        if (!account.IsActive)
        {
            throw new ConflictException(
                "Cannot import a bank statement " +
                "for an inactive account.");
        }

        var currency =
            NormalizeCurrency(dto.Currency);

        if (!string.Equals(
            account.Currency,
            currency,
            StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException(
                "Statement currency must match " +
                "the account currency.");
        }

        var statementReference =
            NormalizeOptionalText(
                dto.StatementReference)?
                .ToUpperInvariant();

        if (!string.IsNullOrWhiteSpace(
            statementReference))
        {
            var duplicateExists =
                await _bankStatementRepository
                    .StatementReferenceExists(
                        dto.AccountId,
                        statementReference);

            if (duplicateExists)
            {
                throw new ConflictException(
                    "A statement with this reference " +
                    "has already been imported for " +
                    "this account.");
            }
        }

        var statementImport =
            new BankStatementImport
            {
                Id =
                    Guid.NewGuid(),

                AccountId =
                    account.Id,

                FileName =
                    dto.FileName.Trim(),

                StatementReference =
                    statementReference,

                Currency =
                    currency,

                StatementFromUtc =
                    NormalizeNullableUtc(
                        dto.StatementFromUtc),

                StatementToUtc =
                    NormalizeNullableUtc(
                        dto.StatementToUtc),

                OpeningBalance =
                    dto.OpeningBalance,

                ClosingBalance =
                    dto.ClosingBalance,

                UploadedByUserId =
                    _currentUserService.UserId,

                UploadedAtUtc =
                    DateTime.UtcNow
            };

        foreach (var lineDto in dto.Lines)
        {
            ValidateLineDto(
                lineDto,
                currency);

            statementImport.Lines.Add(
                new BankStatementLine
                {
                    Id =
                        Guid.NewGuid(),

                    BankStatementImportId =
                        statementImport.Id,

                    AccountId =
                        account.Id,

                    LineNumber =
                        lineDto.LineNumber,

                    TransactionDateUtc =
                        NormalizeUtc(
                            lineDto.TransactionDateUtc),

                    ValueDateUtc =
                        NormalizeNullableUtc(
                            lineDto.ValueDateUtc),

                    Description =
                        lineDto.Description.Trim(),

                    BankReference =
                        NormalizeOptionalText(
                            lineDto.BankReference),

                    CounterpartyName =
                        NormalizeOptionalText(
                            lineDto.CounterpartyName),

                    Amount =
                        lineDto.Amount,

                    Currency =
                        currency,

                    BalanceAfterTransaction =
                        lineDto.BalanceAfterTransaction,

                    ReconciliationStatus =
                        ReconciliationStatus.Unmatched,

                    CreatedAtUtc =
                        DateTime.UtcNow
                });
        }

        statementImport.LineCount =
            statementImport.Lines.Count;

        await _bankStatementRepository.AddImport(
            statementImport);

        await _bankStatementRepository.SaveChanges();

        var savedImport =
            await _bankStatementRepository.GetImportById(
                statementImport.Id);

        var response =
            MapImport(
                savedImport
                ?? statementImport);

        await RecordStatementImportAudit(response);

        return response;
    }

    public async Task<BankStatementImportResponseDto>
        ImportStatementFromCsv(
            CreateBankStatementCsvImportDto dto)
    {
        var lines =
            ParseCsvLines(dto.CsvContent);

        var importDto =
            new CreateBankStatementImportDto
            {
                AccountId =
                    dto.AccountId,

                FileName =
                    dto.FileName,

                StatementReference =
                    dto.StatementReference,

                Currency =
                    dto.Currency,

                StatementFromUtc =
                    dto.StatementFromUtc,

                StatementToUtc =
                    dto.StatementToUtc,

                OpeningBalance =
                    dto.OpeningBalance,

                ClosingBalance =
                    dto.ClosingBalance,

                Lines =
                    lines
            };

        return await ImportStatement(importDto);
    }

    public async Task<BankStatementImportResponseDto>
        ImportStatementFromPdf(
            CreateBankStatementPdfImportDto dto)
    {
        if (dto.PdfContent.Length == 0)
        {
            throw new BusinessRuleException(
                "PDF content is required.");
        }

        var lines =
            ParsePdfLines(
                dto.PdfContent,
                dto.Currency);

        var importDto =
            new CreateBankStatementImportDto
            {
                AccountId =
                    dto.AccountId,

                FileName =
                    dto.FileName,

                StatementReference =
                    dto.StatementReference,

                Currency =
                    dto.Currency,

                StatementFromUtc =
                    dto.StatementFromUtc,

                StatementToUtc =
                    dto.StatementToUtc,

                OpeningBalance =
                    dto.OpeningBalance,

                ClosingBalance =
                    dto.ClosingBalance,

                Lines =
                    lines
            };

        return await ImportStatement(importDto);
    }

    public async Task<BankStatementImportResponseDto>
        GetImport(
            Guid importId)
    {
        var statementImport =
            await _bankStatementRepository.GetImportById(
                importId);

        if (statementImport is null)
        {
            throw new ResourceNotFoundException(
                "Bank statement import not found.");
        }

        return MapImport(
            statementImport);
    }

    public async Task<BankStatementReconciliationSummaryDto>
        GetReconciliationSummary(Guid importId)
    {
        var statementImport =
            await _bankStatementRepository.GetImportById(
                importId);

        if (statementImport is null)
        {
            throw new ResourceNotFoundException(
                "Bank statement import not found.");
        }

        var lines =
            statementImport.Lines.ToList();

        var totalLineCount =
            lines.Count;

        var unmatchedLineCount =
            lines.Count(line =>
                line.ReconciliationStatus ==
                ReconciliationStatus.Unmatched);

        var matchedLineCount =
            lines.Count(line =>
                line.ReconciliationStatus ==
                ReconciliationStatus.Matched);

        var reconciledLineCount =
            lines.Count(line =>
                line.ReconciliationStatus ==
                ReconciliationStatus.Reconciled);

        var ignoredLineCount =
            lines.Count(line =>
                line.ReconciliationStatus ==
                ReconciliationStatus.Ignored);

        var totalInflowAmount =
            lines
                .Where(line =>
                    line.Amount > 0)
                .Sum(line =>
                    line.Amount);

        var totalOutflowAmount =
            lines
                .Where(line =>
                    line.Amount < 0)
                .Sum(line =>
                    Math.Abs(line.Amount));

        var netStatementMovement =
            lines.Sum(line =>
                line.Amount);

        decimal? calculatedClosingBalance =
            null;

        decimal? closingBalanceVariance =
            null;

        if (statementImport.OpeningBalance.HasValue)
        {
            calculatedClosingBalance =
                statementImport.OpeningBalance.Value +
                netStatementMovement;
        }

        if (statementImport.ClosingBalance.HasValue &&
            calculatedClosingBalance.HasValue)
        {
            closingBalanceVariance =
                statementImport.ClosingBalance.Value -
                calculatedClosingBalance.Value;
        }

        /*
        * Reconciled and ignored lines are considered final.
        * Matched lines still need a user to confirm reconciliation.
        */
        var completedLineCount =
            reconciledLineCount + ignoredLineCount;

        var completionPercentage =
            totalLineCount == 0
                ? 0m
                : Math.Round(
                    completedLineCount * 100m / totalLineCount,
                    2);

        return new BankStatementReconciliationSummaryDto
        {
            ImportId =
                statementImport.Id,

            AccountId =
                statementImport.AccountId,

            AccountName =
                statementImport.Account?.Name
                ?? string.Empty,

            FileName =
                statementImport.FileName,

            StatementReference =
                statementImport.StatementReference,

            Currency =
                statementImport.Currency,

            StatementFromUtc =
                statementImport.StatementFromUtc,

            StatementToUtc =
                statementImport.StatementToUtc,

            OpeningBalance =
                statementImport.OpeningBalance,

            ClosingBalance =
                statementImport.ClosingBalance,

            NetStatementMovement =
                netStatementMovement,

            TotalInflowAmount =
                totalInflowAmount,

            TotalOutflowAmount =
                totalOutflowAmount,

            CalculatedClosingBalance =
                calculatedClosingBalance,

            ClosingBalanceVariance =
                closingBalanceVariance,

            TotalLineCount =
                totalLineCount,

            UnmatchedLineCount =
                unmatchedLineCount,

            MatchedLineCount =
                matchedLineCount,

            ReconciledLineCount =
                reconciledLineCount,

            IgnoredLineCount =
                ignoredLineCount,

            MatchedButNotReconciledCount =
                matchedLineCount,

            ActionRequiredLineCount =
                unmatchedLineCount + matchedLineCount,

            ReconciliationCompletionPercentage =
                completionPercentage,

            GeneratedAtUtc =
                DateTime.UtcNow
        };
    }

    public async Task<BankStatementExceptionReportDto>
        GetExceptionReport(Guid importId)
    {
        var statementImport =
            await _bankStatementRepository.GetImportById(
                importId);

        if (statementImport is null)
        {
            throw new ResourceNotFoundException(
                "Bank statement import not found.");
        }

        /*
        * Exception report means lines that still need user action:
        * - Unmatched: needs matching or ignoring.
        * - Matched: needs final reconciliation confirmation.
        */
        var actionRequiredLines =
            statementImport.Lines
                .Where(line =>
                    line.ReconciliationStatus ==
                        ReconciliationStatus.Unmatched ||
                    line.ReconciliationStatus ==
                        ReconciliationStatus.Matched)
                .OrderBy(line =>
                    line.LineNumber)
                .ToList();

        var unmatchedLineCount =
            actionRequiredLines.Count(line =>
                line.ReconciliationStatus ==
                ReconciliationStatus.Unmatched);

        var matchedPendingCount =
            actionRequiredLines.Count(line =>
                line.ReconciliationStatus ==
                ReconciliationStatus.Matched);

        return new BankStatementExceptionReportDto
        {
            ImportId =
                statementImport.Id,

            AccountId =
                statementImport.AccountId,

            AccountName =
                statementImport.Account?.Name
                ?? string.Empty,

            FileName =
                statementImport.FileName,

            StatementReference =
                statementImport.StatementReference,

            Currency =
                statementImport.Currency,

            GeneratedAtUtc =
                DateTime.UtcNow,

            ActionRequiredLineCount =
                actionRequiredLines.Count,

            UnmatchedLineCount =
                unmatchedLineCount,

            MatchedPendingReconciliationCount =
                matchedPendingCount,

            Lines =
                actionRequiredLines
                    .Select(MapLine)
                    .ToList()
        };
    }

    public async Task<BookSideExceptionReportDto>
        GetBookSideExceptionReport(Guid importId)
    {
        var statementImport =
            await _bankStatementRepository.GetImportById(
                importId);

        if (statementImport is null)
        {
            throw new ResourceNotFoundException(
                "Bank statement import not found.");
        }

        var transactions =
            await _transactionRepository
                .GetUnmatchedCompletedTransactionsForReconciliation(
                    statementImport.AccountId,
                    statementImport.Currency,
                    statementImport.StatementFromUtc,
                    statementImport.StatementToUtc);

        var mappedTransactions =
            transactions
                .Select(transaction =>
                    MapBookSideTransaction(
                        transaction,
                        statementImport.AccountId))
                .ToList();

        var totalInflowAmount =
            mappedTransactions
                .Where(transaction =>
                    transaction.SignedAmount > 0)
                .Sum(transaction =>
                    transaction.SignedAmount);

        var totalOutflowAmount =
            mappedTransactions
                .Where(transaction =>
                    transaction.SignedAmount < 0)
                .Sum(transaction =>
                    Math.Abs(transaction.SignedAmount));

        return new BookSideExceptionReportDto
        {
            ImportId =
                statementImport.Id,

            AccountId =
                statementImport.AccountId,

            AccountName =
                statementImport.Account?.Name
                ?? string.Empty,

            FileName =
                statementImport.FileName,

            StatementReference =
                statementImport.StatementReference,

            Currency =
                statementImport.Currency,

            StatementFromUtc =
                statementImport.StatementFromUtc,

            StatementToUtc =
                statementImport.StatementToUtc,

            GeneratedAtUtc =
                DateTime.UtcNow,

            UnmatchedTransactionCount =
                mappedTransactions.Count,

            NetUnmatchedAmount =
                mappedTransactions.Sum(transaction =>
                    transaction.SignedAmount),

            TotalUnmatchedInflowAmount =
                totalInflowAmount,

            TotalUnmatchedOutflowAmount =
                totalOutflowAmount,

            Transactions =
                mappedTransactions
        };
    }

    public async Task<List<BankStatementLineResponseDto>>
        GetUnmatchedLines(
            Guid? accountId,
            DateTime? fromUtc,
            DateTime? toUtc)
    {
        if (fromUtc.HasValue &&
            toUtc.HasValue &&
            fromUtc.Value > toUtc.Value)
        {
            throw new BusinessRuleException(
                "The start date cannot be later " +
                "than the end date.");
        }

        var lines =
            await _bankStatementRepository
                .GetUnmatchedLines(
                    accountId,
                    NormalizeNullableUtc(fromUtc),
                    NormalizeNullableUtc(toUtc));

        return lines
            .Select(MapLine)
            .ToList();
    }

    public async Task<BankStatementReconciliationResultDto>
        AutoMatchImport(
            Guid importId,
            int dateToleranceDays = 2)
    {
        ValidateDateToleranceDays(
            dateToleranceDays);

        var statementImport =
            await _bankStatementRepository.GetImportById(
                importId);

        if (statementImport is null)
        {
            throw new ResourceNotFoundException(
                "Bank statement import not found.");
        }

        var processedAtUtc =
            DateTime.UtcNow;

        var candidateLines =
            statementImport.Lines
                .Where(line =>
                    line.ReconciliationStatus ==
                    ReconciliationStatus.Unmatched)
                .OrderBy(line =>
                    line.LineNumber)
                .ToList();

        var result =
            new BankStatementReconciliationResultDto
            {
                ImportId =
                    statementImport.Id,

                ProcessedAtUtc =
                    processedAtUtc,

                CandidateLineCount =
                    candidateLines.Count
            };

        /*
        * This prevents two lines in the same run from
        * being matched to the same treasury transaction
        * before SaveChanges commits the updates.
        */
        var usedTransactionIds =
            new HashSet<Guid>();

        foreach (var line in candidateLines)
        {
            var potentialMatches =
                await _transactionRepository
                    .FindPotentialReconciliationMatches(
                        line.AccountId,
                        line.Amount,
                        line.Currency,
                        line.TransactionDateUtc,
                        dateToleranceDays);

            potentialMatches =
                potentialMatches
                    .Where(transaction =>
                        !usedTransactionIds
                            .Contains(transaction.Id))
                    .ToList();

            if (potentialMatches.Count == 1)
            {
                var matchedTransaction =
                    potentialMatches.Single();

                line.ReconciliationStatus =
                    ReconciliationStatus.Matched;

                line.MatchedTreasuryTransactionId =
                    matchedTransaction.Id;

                line.MatchedAtUtc =
                    processedAtUtc;

                line.ConcurrencyToken =
                    Guid.NewGuid();

                _bankStatementRepository.UpdateLine(
                    line);

                usedTransactionIds.Add(
                    matchedTransaction.Id);

                result.MatchedLineCount++;

                result.MatchedLineIds.Add(
                    line.Id);

                continue;
            }

            if (potentialMatches.Count == 0)
            {
                result.UnmatchedLineCount++;
            }
            else
            {
                /*
                * More than one possible treasury transaction
                * means the system should not guess.
                * A later manual-reconciliation step will handle this.
                */
                result.AmbiguousMatchCount++;
            }
        }

        await _bankStatementRepository.SaveChanges();

        if (result.MatchedLineCount > 0)
        {
            await RecordAutoMatchAudit(
                statementImport,
                result,
                dateToleranceDays);
        }

        return result;
    }

    public async Task<BankStatementLineResponseDto>
        ManualMatchLine(
            Guid lineId,
            Guid treasuryTransactionId)
    {
        if (treasuryTransactionId == Guid.Empty)
        {
            throw new BusinessRuleException(
                "Treasury transaction is required.");
        }

        var line =
            await _bankStatementRepository.GetLineById(
                lineId);

        if (line is null)
        {
            throw new ResourceNotFoundException(
                "Bank statement line not found.");
        }

        if (line.ReconciliationStatus ==
            ReconciliationStatus.Reconciled)
        {
            throw new ConflictException(
                "A reconciled statement line cannot " +
                "be rematched.");
        }

        if (line.ReconciliationStatus ==
            ReconciliationStatus.Ignored)
        {
            throw new ConflictException(
                "An ignored statement line cannot " +
                "be matched.");
        }

        if (line.ReconciliationStatus ==
            ReconciliationStatus.Matched)
        {
            throw new ConflictException(
                "This statement line is already matched. " +
                "Unmatch it before assigning another " +
                "treasury transaction.");
        }

        var transaction =
            await _transactionRepository.GetById(
                treasuryTransactionId);

        if (transaction is null)
        {
            throw new ResourceNotFoundException(
                "Treasury transaction not found.");
        }

        EnsureTransactionCanMatchLine(
            line,
            transaction);

        var alreadyMatched =
            await _bankStatementRepository
                .TransactionAlreadyMatched(
                    treasuryTransactionId,
                    line.Id);

        if (alreadyMatched)
        {
            throw new ConflictException(
                "This treasury transaction has already " +
                "been matched to another bank statement line.");
        }

        var beforeValues =
            SnapshotLine(line);

        line.ReconciliationStatus =
            ReconciliationStatus.Matched;

        line.MatchedTreasuryTransactionId =
            transaction.Id;

        line.MatchedAtUtc =
            DateTime.UtcNow;

        line.ConcurrencyToken =
            Guid.NewGuid();

        _bankStatementRepository.UpdateLine(
            line);

        await _bankStatementRepository.SaveChanges();

        var savedLine =
            await _bankStatementRepository.GetLineById(
                line.Id);

        var response =
            MapLine(savedLine ?? line);

        await RecordLineAudit(
            AuditActionTypes.Matched,
            $"Bank statement line {response.LineNumber} was manually matched.",
            beforeValues,
            response,
            new
            {
                Module = "Bank Statement Reconciliation",
                MatchType = "Manual",
                TreasuryTransactionId = transaction.Id
            });

        return response;
    }

    public async Task<BankStatementLineResponseDto>
        ReconcileLine(
            Guid lineId)
    {
        var line =
            await _bankStatementRepository.GetLineById(
                lineId);

        if (line is null)
        {
            throw new ResourceNotFoundException(
                "Bank statement line not found.");
        }

        if (line.ReconciliationStatus ==
            ReconciliationStatus.Reconciled)
        {
            throw new ConflictException(
                "This statement line is already reconciled.");
        }

        if (line.ReconciliationStatus !=
            ReconciliationStatus.Matched ||
            line.MatchedTreasuryTransactionId is null)
        {
            throw new ConflictException(
                "Only a matched statement line can " +
                "be reconciled.");
        }

        var beforeValues =
            SnapshotLine(line);

        line.ReconciliationStatus =
            ReconciliationStatus.Reconciled;

        line.ReconciledByUserId =
            _currentUserService.UserId;

        line.ReconciledAtUtc =
            DateTime.UtcNow;

        line.ConcurrencyToken =
            Guid.NewGuid();

        _bankStatementRepository.UpdateLine(
            line);

        await _bankStatementRepository.SaveChanges();

        var savedLine =
            await _bankStatementRepository.GetLineById(
                line.Id);

        var response =
            MapLine(savedLine ?? line);

        await RecordLineAudit(
            AuditActionTypes.Reconciled,
            $"Bank statement line {response.LineNumber} was reconciled.",
            beforeValues,
            response,
            new
            {
                Module = "Bank Statement Reconciliation",
                ReconciliationAction = "Reconciled"
            });

        return response;
    }

    public async Task<BankStatementLineResponseDto>
        UnmatchLine(
            Guid lineId)
    {
        var line =
            await _bankStatementRepository.GetLineById(
                lineId);

        if (line is null)
        {
            throw new ResourceNotFoundException(
                "Bank statement line not found.");
        }

        if (line.ReconciliationStatus ==
            ReconciliationStatus.Reconciled)
        {
            throw new ConflictException(
                "A reconciled statement line cannot " +
                "be unmatched.");
        }

        if (line.ReconciliationStatus !=
            ReconciliationStatus.Matched)
        {
            throw new ConflictException(
                "Only a matched statement line can " +
                "be unmatched.");
        }

        var previousTreasuryTransactionId =
            line.MatchedTreasuryTransactionId;

        var beforeValues =
            SnapshotLine(line);

        line.ReconciliationStatus =
            ReconciliationStatus.Unmatched;

        line.MatchedTreasuryTransactionId =
            null;

        line.MatchedAtUtc =
            null;

        line.ReconciledByUserId =
            null;

        line.ReconciledAtUtc =
            null;

        line.ConcurrencyToken =
            Guid.NewGuid();

        _bankStatementRepository.UpdateLine(
            line);

        await _bankStatementRepository.SaveChanges();

        var savedLine =
            await _bankStatementRepository.GetLineById(
                line.Id);

        var response =
            MapLine(savedLine ?? line);

        await RecordLineAudit(
            AuditActionTypes.Updated,
            $"Bank statement line {response.LineNumber} was unmatched.",
            beforeValues,
            response,
            new
            {
                Module = "Bank Statement Reconciliation",
                ReconciliationAction = "Unmatched",
                PreviousTreasuryTransactionId = previousTreasuryTransactionId
            });

        return response;
    }

    public async Task<BankStatementLineResponseDto>
        IgnoreLine(
            Guid lineId)
    {
        var line =
            await _bankStatementRepository.GetLineById(
                lineId);

        if (line is null)
        {
            throw new ResourceNotFoundException(
                "Bank statement line not found.");
        }

        if (line.ReconciliationStatus ==
            ReconciliationStatus.Reconciled)
        {
            throw new ConflictException(
                "A reconciled statement line cannot " +
                "be ignored.");
        }

        if (line.ReconciliationStatus ==
            ReconciliationStatus.Matched)
        {
            throw new ConflictException(
                "Unmatch the statement line before " +
                "ignoring it.");
        }

        var beforeValues =
            SnapshotLine(line);

        line.ReconciliationStatus =
            ReconciliationStatus.Ignored;

        line.MatchedTreasuryTransactionId =
            null;

        line.MatchedAtUtc =
            null;

        line.ReconciledByUserId =
            null;

        line.ReconciledAtUtc =
            null;

        line.ConcurrencyToken =
            Guid.NewGuid();

        _bankStatementRepository.UpdateLine(
            line);

        await _bankStatementRepository.SaveChanges();

        var savedLine =
            await _bankStatementRepository.GetLineById(
                line.Id);

        var response =
            MapLine(savedLine ?? line);

        await RecordLineAudit(
            AuditActionTypes.Ignored,
            $"Bank statement line {response.LineNumber} was ignored.",
            beforeValues,
            response,
            new
            {
                Module = "Bank Statement Reconciliation",
                ReconciliationAction = "Ignored"
            });

        return response;
    }

    private async Task RecordStatementImportAudit(
        BankStatementImportResponseDto statementImport)
    {
        await _auditLogService.Record(
            new CreateAuditLogDto
            {
                Action =
                    AuditActionTypes.Imported,

                EntityType =
                    AuditEntityTypes.BankStatementImport,

                EntityId =
                    statementImport.Id,

                EntityReference =
                    statementImport.StatementReference
                    ?? statementImport.FileName,

                Summary =
                    $"Bank statement {statementImport.FileName} " +
                    $"was imported with {statementImport.LineCount} line(s).",

                AfterValues =
                    SnapshotImport(statementImport),

                Metadata =
                    new
                    {
                        Module = "Bank Statement Reconciliation",
                        statementImport.AccountId,
                        statementImport.AccountName
                    }
            });
    }

    private async Task RecordAutoMatchAudit(
        BankStatementImport statementImport,
        BankStatementReconciliationResultDto result,
        int dateToleranceDays)
    {
        await _auditLogService.Record(
            new CreateAuditLogDto
            {
                Action =
                    AuditActionTypes.Matched,

                EntityType =
                    AuditEntityTypes.BankStatementImport,

                EntityId =
                    statementImport.Id,

                EntityReference =
                    statementImport.StatementReference
                    ?? statementImport.FileName,

                Summary =
                    $"Auto-match completed for bank statement " +
                    $"{statementImport.FileName}; " +
                    $"{result.MatchedLineCount} line(s) matched.",

                Metadata =
                    new
                    {
                        Module = "Bank Statement Reconciliation",
                        MatchType = "Automatic",
                        DateToleranceDays = dateToleranceDays,
                        result.CandidateLineCount,
                        result.MatchedLineCount,
                        result.UnmatchedLineCount,
                        result.AmbiguousMatchCount,
                        result.MatchedLineIds
                    }
            });
    }

    private async Task RecordLineAudit(
        string action,
        string summary,
        object? beforeValues,
        BankStatementLineResponseDto line,
        object? metadata)
    {
        await _auditLogService.Record(
            new CreateAuditLogDto
            {
                Action =
                    action,

                EntityType =
                    AuditEntityTypes.BankStatementLine,

                EntityId =
                    line.Id,

                EntityReference =
                    line.BankReference
                    ?? $"Line {line.LineNumber}",

                Summary =
                    summary,

                BeforeValues =
                    beforeValues,

                AfterValues =
                    SnapshotLine(line),

                Metadata =
                    metadata
            });
    }

    private static object SnapshotImport(
        BankStatementImportResponseDto statementImport)
    {
        return new
        {
            statementImport.Id,
            statementImport.AccountId,
            statementImport.AccountName,
            statementImport.FileName,
            statementImport.StatementReference,
            statementImport.Currency,
            statementImport.StatementFromUtc,
            statementImport.StatementToUtc,
            statementImport.OpeningBalance,
            statementImport.ClosingBalance,
            statementImport.LineCount,
            statementImport.UploadedByUserId,
            statementImport.UploadedAtUtc
        };
    }

    private static object SnapshotLine(
        BankStatementLine line)
    {
        return new
        {
            line.Id,
            line.BankStatementImportId,
            line.AccountId,
            line.LineNumber,
            line.TransactionDateUtc,
            line.ValueDateUtc,
            line.Description,
            line.BankReference,
            line.CounterpartyName,
            line.Amount,
            line.Currency,
            line.BalanceAfterTransaction,
            line.ReconciliationStatus,
            line.MatchedTreasuryTransactionId,
            line.MatchedAtUtc,
            line.ReconciledByUserId,
            line.ReconciledAtUtc,
            line.CreatedAtUtc
        };
    }

    private static object SnapshotLine(
        BankStatementLineResponseDto line)
    {
        return new
        {
            line.Id,
            line.BankStatementImportId,
            line.AccountId,
            line.LineNumber,
            line.TransactionDateUtc,
            line.ValueDateUtc,
            line.Description,
            line.BankReference,
            line.CounterpartyName,
            line.Amount,
            line.Currency,
            line.BalanceAfterTransaction,
            line.ReconciliationStatus,
            line.MatchedTreasuryTransactionId,
            line.MatchedTreasuryTransactionReference,
            line.MatchedAtUtc,
            line.ReconciledByUserId,
            line.ReconciledAtUtc,
            line.CreatedAtUtc
        };
    }

    private static void ValidateImportDto(
        CreateBankStatementImportDto dto)
    {
        if (dto.AccountId == Guid.Empty)
        {
            throw new BusinessRuleException(
                "Account is required.");
        }

        if (string.IsNullOrWhiteSpace(
            dto.FileName))
        {
            throw new BusinessRuleException(
                "File name is required.");
        }

        if (string.IsNullOrWhiteSpace(
            dto.Currency))
        {
            throw new BusinessRuleException(
                "Currency is required.");
        }

        if (dto.Lines.Count == 0)
        {
            throw new BusinessRuleException(
                "At least one statement line is required.");
        }

        if (dto.Lines.Count > MaximumLineCount)
        {
            throw new BusinessRuleException(
                $"A statement import cannot exceed " +
                $"{MaximumLineCount} lines.");
        }

        if (dto.StatementFromUtc.HasValue &&
            dto.StatementToUtc.HasValue &&
            dto.StatementFromUtc.Value >
            dto.StatementToUtc.Value)
        {
            throw new BusinessRuleException(
                "Statement start date cannot be later " +
                "than statement end date.");
        }

        var duplicateLineNumber =
            dto.Lines
                .GroupBy(line =>
                    line.LineNumber)
                .FirstOrDefault(group =>
                    group.Count() > 1);

        if (duplicateLineNumber is not null)
        {
            throw new BusinessRuleException(
                $"Statement line number " +
                $"{duplicateLineNumber.Key} appears " +
                "more than once.");
        }
    }

    private static void ValidateLineDto(
        CreateBankStatementLineDto dto,
        string statementCurrency)
    {
        if (dto.LineNumber <= 0)
        {
            throw new BusinessRuleException(
                "Statement line number must be greater " +
                "than zero.");
        }

        if (string.IsNullOrWhiteSpace(
            dto.Description))
        {
            throw new BusinessRuleException(
                "Statement line description is required.");
        }

        if (dto.Amount == 0)
        {
            throw new BusinessRuleException(
                "Statement line amount cannot be zero.");
        }

        var lineCurrency =
            NormalizeCurrency(
                dto.Currency);

        if (!string.Equals(
            lineCurrency,
            statementCurrency,
            StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException(
                "All statement lines must use the " +
                "same currency as the statement.");
        }
    }

    private static void ValidateDateToleranceDays(
        int dateToleranceDays)
    {
        if (dateToleranceDays < 0 ||
            dateToleranceDays > 10)
        {
            throw new BusinessRuleException(
                "Date tolerance must be between " +
                "0 and 10 days.");
        }
    }

    private static void EnsureTransactionCanMatchLine(
        BankStatementLine line,
        TreasuryTransaction transaction)
    {
        if (transaction.Status !=
            TransactionStatuses.Completed)
        {
            throw new ConflictException(
                "Only completed treasury transactions " +
                "can be reconciled.");
        }

        if (!string.Equals(
            transaction.Currency,
            line.Currency,
            StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException(
                "The treasury transaction currency does " +
                "not match the bank statement line.");
        }

        if (transaction.Amount !=
            Math.Abs(line.Amount))
        {
            throw new BusinessRuleException(
                "The treasury transaction amount does " +
                "not match the bank statement line.");
        }

        /*
        * Positive bank line means cash came into this account.
        * Negative bank line means cash left this account.
        */
        if (line.Amount > 0 &&
            transaction.DestinationAccountId != line.AccountId)
        {
            throw new BusinessRuleException(
                "The treasury transaction does not " +
                "represent cash coming into this account.");
        }

        if (line.Amount < 0 &&
            transaction.SourceAccountId != line.AccountId)
        {
            throw new BusinessRuleException(
                "The treasury transaction does not " +
                "represent cash leaving this account.");
        }
    }

    private static List<CreateBankStatementLineDto> ParseCsvLines(
        string csvContent)
    {
        if (string.IsNullOrWhiteSpace(csvContent))
        {
            throw new BusinessRuleException(
                "CSV content is required.");
        }

        using var reader =
            new StringReader(csvContent);

        var headerLine =
            reader.ReadLine();

        if (string.IsNullOrWhiteSpace(headerLine))
        {
            throw new BusinessRuleException(
                "CSV header row is required.");
        }

        var headers =
            ParseCsvRow(headerLine);

        var lines =
            new List<CreateBankStatementLineDto>();

        var physicalRowNumber =
            1;

        string? rowLine;

        while ((rowLine = reader.ReadLine()) is not null)
        {
            physicalRowNumber++;

            if (string.IsNullOrWhiteSpace(rowLine))
            {
                continue;
            }

            var values =
                ParseCsvRow(rowLine);

            if (values.Count != headers.Count)
            {
                throw new BusinessRuleException(
                    $"CSV row {physicalRowNumber} has " +
                    $"{values.Count} columns, but the header " +
                    $"has {headers.Count} columns.");
            }

            var row =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase);

            for (var index = 0; index < headers.Count; index++)
            {
                row[headers[index].Trim()] =
                    values[index].Trim();
            }

            lines.Add(
                new CreateBankStatementLineDto
                {
                    LineNumber =
                        ParseRequiredInt(
                            GetRequiredCsvValue(
                                row,
                                "LineNumber"),
                            "LineNumber"),

                    TransactionDateUtc =
                        ParseRequiredDate(
                            GetRequiredCsvValue(
                                row,
                                "TransactionDateUtc"),
                            "TransactionDateUtc"),

                    ValueDateUtc =
                        ParseOptionalDate(
                            GetOptionalCsvValue(
                                row,
                                "ValueDateUtc"),
                            "ValueDateUtc"),

                    Description =
                        GetRequiredCsvValue(
                            row,
                            "Description"),

                    BankReference =
                        GetOptionalCsvValue(
                            row,
                            "BankReference"),

                    CounterpartyName =
                        GetOptionalCsvValue(
                            row,
                            "CounterpartyName"),

                    Amount =
                        ParseRequiredDecimal(
                            GetRequiredCsvValue(
                                row,
                                "Amount"),
                            "Amount"),

                    Currency =
                        GetRequiredCsvValue(
                            row,
                            "Currency"),

                    BalanceAfterTransaction =
                        ParseOptionalDecimal(
                            GetOptionalCsvValue(
                                row,
                                "BalanceAfterTransaction"),
                            "BalanceAfterTransaction")
                });
        }

        if (lines.Count == 0)
        {
            throw new BusinessRuleException(
                "CSV file must contain at least one data row.");
        }

        return lines;
    }

    private static List<string> ParseCsvRow(
        string row)
    {
        var values =
            new List<string>();

        var currentValue =
            new StringBuilder();

        var insideQuotes =
            false;

        for (var index = 0; index < row.Length; index++)
        {
            var character =
                row[index];

            if (character == '"')
            {
                if (insideQuotes &&
                    index + 1 < row.Length &&
                    row[index + 1] == '"')
                {
                    currentValue.Append('"');

                    index++;

                    continue;
                }

                insideQuotes =
                    !insideQuotes;

                continue;
            }

            if (character == ',' &&
                !insideQuotes)
            {
                values.Add(
                    currentValue.ToString());

                currentValue.Clear();

                continue;
            }

            currentValue.Append(character);
        }

        if (insideQuotes)
        {
            throw new BusinessRuleException(
                "CSV row contains an unclosed quote.");
        }

        values.Add(
            currentValue.ToString());

        return values;
    }

    private static string GetRequiredCsvValue(
        Dictionary<string, string> row,
        string columnName)
    {
        if (!row.TryGetValue(columnName, out var value) ||
            string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessRuleException(
                $"CSV column '{columnName}' is required.");
        }

        return value.Trim();
    }

    private static string? GetOptionalCsvValue(
        Dictionary<string, string> row,
        string columnName)
    {
        if (!row.TryGetValue(columnName, out var value) ||
            string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private const string PdfDatePattern =
        @"(?:\d{4}-\d{2}-\d{2}|\d{2}[/-]\d{2}[/-]\d{4}|\d{2}\s+[A-Za-z]{3,9}\s+\d{4}|\d{2}-[A-Za-z]{3,9}-\d{4})";

    private const string PdfAmountPattern =
        @"[+-]?\(?\d[\d,]*(?:\.\d{1,2})?\)?(?:CR|DR)?";

    private static readonly Regex PdfTransactionLineRegex =
        new(
            @"^\s*(?<transactionDate>" + PdfDatePattern + @")\s+" +
            @"(?:(?<valueDate>" + PdfDatePattern + @")\s+)?" +
            @"(?<description>.+?)\s+" +
            @"(?<amount>" + PdfAmountPattern + @")\s+" +
            @"(?<balance>" + PdfAmountPattern + @")\s*$",
            RegexOptions.IgnoreCase |
            RegexOptions.Compiled);

    private static List<CreateBankStatementLineDto> ParsePdfLines(
        byte[] pdfContent,
        string currency)
    {
        var statementCurrency =
            NormalizeCurrency(currency);

        var extractedText =
            ExtractPdfText(pdfContent);

        var physicalLines =
            extractedText
                .Split(
                    ["\r\n", "\n"],
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(line =>
                    Regex.Replace(line.Trim(), @"\s+", " "))
                .Where(line =>
                    !string.IsNullOrWhiteSpace(line))
                .ToList();

        var parsedLines =
            new List<CreateBankStatementLineDto>();

        foreach (var line in physicalLines)
        {
            var match =
                PdfTransactionLineRegex.Match(line);

            if (!match.Success)
            {
                continue;
            }

            parsedLines.Add(
                new CreateBankStatementLineDto
                {
                    LineNumber =
                        parsedLines.Count + 1,

                    TransactionDateUtc =
                        ParsePdfDate(
                            match.Groups["transactionDate"].Value),

                    ValueDateUtc =
                        match.Groups["valueDate"].Success
                            ? ParsePdfDate(
                                match.Groups["valueDate"].Value)
                            : null,

                    Description =
                        match.Groups["description"].Value.Trim(),

                    BankReference =
                        null,

                    CounterpartyName =
                        null,

                    Amount =
                        ParsePdfAmount(
                            match.Groups["amount"].Value),

                    Currency =
                        statementCurrency,

                    BalanceAfterTransaction =
                        ParsePdfAmount(
                            match.Groups["balance"].Value)
                });
        }

        if (parsedLines.Count == 0)
        {
            throw new BusinessRuleException(
                "No transaction lines could be read from the PDF. " +
                "Ensure the PDF is text-based and each transaction line " +
                "contains date, description, amount, and balance.");
        }

        return parsedLines;
    }

    private static string ExtractPdfText(
        byte[] pdfContent)
    {
        using var stream =
            new MemoryStream(pdfContent);

        using var document =
            PdfDocument.Open(stream);

        var builder =
            new StringBuilder();

        foreach (var page in document.GetPages())
        {
            builder.AppendLine(
                ContentOrderTextExtractor.GetText(page));
        }

        var text =
            builder.ToString();

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new BusinessRuleException(
                "No readable text was found in the PDF. " +
                "Scanned PDFs require OCR and are not supported yet.");
        }

        return text;
    }

    private static DateTime ParsePdfDate(
        string value)
    {
        string[] formats =
        [
            "yyyy-MM-dd",
            "dd/MM/yyyy",
            "dd-MM-yyyy",
            "MM/dd/yyyy",
            "MM-dd-yyyy",
            "dd MMM yyyy",
            "dd MMMM yyyy",
            "dd-MMM-yyyy",
            "dd-MMMM-yyyy"
        ];

        if (DateTime.TryParseExact(
            value.Trim(),
            formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal |
            DateTimeStyles.AdjustToUniversal,
            out var result))
        {
            return result;
        }

        throw new BusinessRuleException(
            $"PDF date '{value}' is not in a supported format.");
    }

    private static decimal ParsePdfAmount(
        string value)
    {
        var normalized =
            value.Trim();

        var isNegative =
            normalized.StartsWith("-") ||
            normalized.StartsWith("(") ||
            normalized.EndsWith(
                "DR",
                StringComparison.OrdinalIgnoreCase);

        normalized =
            normalized
                .Replace(",", string.Empty)
                .Replace("(", string.Empty)
                .Replace(")", string.Empty)
                .Replace("+", string.Empty)
                .Replace("-", string.Empty)
                .Replace("CR", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("DR", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Trim();

        if (!decimal.TryParse(
            normalized,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var amount))
        {
            throw new BusinessRuleException(
                $"PDF amount '{value}' is not a valid amount.");
        }

        return isNegative
            ? -amount
            : amount;
    }

    private static int ParseRequiredInt(
        string value,
        string fieldName)
    {
        if (int.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var result))
        {
            return result;
        }

        throw new BusinessRuleException(
            $"CSV column '{fieldName}' must be a valid number.");
    }

    private static decimal ParseRequiredDecimal(
        string value,
        string fieldName)
    {
        if (decimal.TryParse(
            value,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var result))
        {
            return result;
        }

        throw new BusinessRuleException(
            $"CSV column '{fieldName}' must be a valid amount.");
    }

    private static decimal? ParseOptionalDecimal(
        string? value,
        string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return ParseRequiredDecimal(
            value,
            fieldName);
    }

    private static DateTime ParseRequiredDate(
        string value,
        string fieldName)
    {
        if (DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal |
            DateTimeStyles.AdjustToUniversal,
            out var result))
        {
            return result;
        }

        throw new BusinessRuleException(
            $"CSV column '{fieldName}' must be a valid date.");
    }

    private static DateTime? ParseOptionalDate(
        string? value,
        string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return ParseRequiredDate(
            value,
            fieldName);
    }

    private static string NormalizeCurrency(
        string currency)
    {
        var normalized =
            currency.Trim().ToUpperInvariant();

        if (normalized.Length != 3)
        {
            throw new BusinessRuleException(
                "Currency must be a 3-letter code.");
        }

        return normalized;
    }

    private static string? NormalizeOptionalText(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static DateTime NormalizeUtc(
        DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc)
        {
            return value;
        }

        if (value.Kind == DateTimeKind.Local)
        {
            return value.ToUniversalTime();
        }

        return DateTime.SpecifyKind(
            value,
            DateTimeKind.Utc);
    }

    private static DateTime? NormalizeNullableUtc(
        DateTime? value)
    {
        return value.HasValue
            ? NormalizeUtc(value.Value)
            : null;
    }

    private static BankStatementImportResponseDto MapImport(
        BankStatementImport statementImport)
    {
        return new BankStatementImportResponseDto
        {
            Id =
                statementImport.Id,

            AccountId =
                statementImport.AccountId,

            AccountName =
                statementImport.Account?.Name
                ?? string.Empty,

            FileName =
                statementImport.FileName,

            StatementReference =
                statementImport.StatementReference,

            Currency =
                statementImport.Currency,

            StatementFromUtc =
                statementImport.StatementFromUtc,

            StatementToUtc =
                statementImport.StatementToUtc,

            OpeningBalance =
                statementImport.OpeningBalance,

            ClosingBalance =
                statementImport.ClosingBalance,

            LineCount =
                statementImport.LineCount,

            UploadedByUserId =
                statementImport.UploadedByUserId,

            UploadedAtUtc =
                statementImport.UploadedAtUtc,

            Lines =
                statementImport.Lines
                    .OrderBy(line =>
                        line.LineNumber)
                    .Select(MapLine)
                    .ToList()
        };
    }

    private static BankStatementLineResponseDto MapLine(
        BankStatementLine line)
    {
        return new BankStatementLineResponseDto
        {
            Id =
                line.Id,

            BankStatementImportId =
                line.BankStatementImportId,

            AccountId =
                line.AccountId,

            LineNumber =
                line.LineNumber,

            TransactionDateUtc =
                line.TransactionDateUtc,

            ValueDateUtc =
                line.ValueDateUtc,

            Description =
                line.Description,

            BankReference =
                line.BankReference,

            CounterpartyName =
                line.CounterpartyName,

            Amount =
                line.Amount,

            Currency =
                line.Currency,

            BalanceAfterTransaction =
                line.BalanceAfterTransaction,

            ReconciliationStatus =
                line.ReconciliationStatus,

            MatchedTreasuryTransactionId =
                line.MatchedTreasuryTransactionId,

            MatchedTreasuryTransactionReference =
                line.MatchedTreasuryTransaction?.Reference,

            MatchedAtUtc =
                line.MatchedAtUtc,

            ReconciledByUserId =
                line.ReconciledByUserId,

            ReconciledAtUtc =
                line.ReconciledAtUtc,

            CreatedAtUtc =
                line.CreatedAtUtc
        };
    }

    private static UnmatchedTreasuryTransactionDto
        MapBookSideTransaction(
            TreasuryTransaction transaction,
            Guid accountId)
    {
        var isInflow =
            transaction.DestinationAccountId == accountId;

        var signedAmount =
            isInflow
                ? transaction.Amount
                : -transaction.Amount;

        return new UnmatchedTreasuryTransactionDto
        {
            Id =
                transaction.Id,

            Reference =
                transaction.Reference,

            TransactionType =
                transaction.TransactionType,

            Status =
                transaction.Status,

            SourceAccountId =
                transaction.SourceAccountId,

            DestinationAccountId =
                transaction.DestinationAccountId,

            CashDirection =
                isInflow
                    ? "Inflow"
                    : "Outflow",

            Amount =
                transaction.Amount,

            SignedAmount =
                signedAmount,

            Currency =
                transaction.Currency,

            Description =
                transaction.Description,

            Category =
                transaction.Category,

            CounterpartyName =
                transaction.CounterpartyName,

            ExternalReference =
                transaction.ExternalReference,

            CreatedAtUtc =
                transaction.CreatedAtUtc,

            CompletedAtUtc =
                transaction.CompletedAtUtc
        };
    }
}
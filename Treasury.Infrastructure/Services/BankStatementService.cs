using Treasury.Application.Common.Exceptions;
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

    public BankStatementService(
        IBankStatementRepository bankStatementRepository,
        IAccountRepository accountRepository,
        ICurrentUserService currentUserService,
        ITreasuryTransactionRepository transactionRepository)
    {
        _bankStatementRepository =
            bankStatementRepository;

        _accountRepository =
            accountRepository;

        _currentUserService =
            currentUserService;
        
        _transactionRepository =
            transactionRepository;
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

        return MapImport(
            savedImport
            ?? statementImport);
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

        return MapLine(
            savedLine ?? line);
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

        return MapLine(
            savedLine ?? line);
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

        return MapLine(
            savedLine ?? line);
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

        return MapLine(
            savedLine ?? line);
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
}
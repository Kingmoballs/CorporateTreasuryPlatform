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

    public BankStatementService(
        IBankStatementRepository bankStatementRepository,
        IAccountRepository accountRepository,
        ICurrentUserService currentUserService)
    {
        _bankStatementRepository =
            bankStatementRepository;

        _accountRepository =
            accountRepository;

        _currentUserService =
            currentUserService;
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
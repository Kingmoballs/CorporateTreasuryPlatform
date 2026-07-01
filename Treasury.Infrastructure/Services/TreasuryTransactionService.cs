using Treasury.Application.DTOs.Transactions;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;

namespace Treasury.Infrastructure.Services;

public class TreasuryTransactionService
    : ITreasuryTransactionService
{
    private const int MaximumPageSize = 100;

    private readonly ITreasuryTransactionRepository
        _transactionRepository;

    public TreasuryTransactionService(
        ITreasuryTransactionRepository
            transactionRepository)
    {
        _transactionRepository =
            transactionRepository;
    }

    public async Task<PagedTreasuryTransactionsDto>
        SearchTransactions(
            TransactionQueryDto query)
    {
        ValidateQuery(query);

        query.FromUtc =
            NormalizeDateTime(query.FromUtc);

        query.ToUtc =
            NormalizeDateTime(query.ToUtc);

        if (query.FromUtc.HasValue &&
            query.ToUtc.HasValue &&
            query.FromUtc >= query.ToUtc)
        {
            throw new ArgumentException(
                "The start date must be earlier " +
                "than the end date.");
        }

        var result =
            await _transactionRepository
                .Search(query);

        var items = result.Items
            .Select(MapSummary)
            .ToList();

        return new PagedTreasuryTransactionsDto
        {
            Page = query.Page,

            PageSize = query.PageSize,

            TotalCount = result.TotalCount,

            TotalPages = result.TotalCount == 0
                ? 0
                : (int)Math.Ceiling(
                    result.TotalCount /
                    (double)query.PageSize),

            Items = items
        };
    }

    public async Task<TreasuryTransactionDetailDto>
        GetByReference(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            throw new ArgumentException(
                "Transaction reference is required.");
        }

        var transaction =
            await _transactionRepository
                .GetByReference(
                    reference
                        .Trim()
                        .ToUpperInvariant());

        if (transaction is null)
        {
            throw new KeyNotFoundException(
                "Transaction not found.");
        }

        return new TreasuryTransactionDetailDto
        {
            Id = transaction.Id,

            Reference = transaction.Reference,

            TransactionType =
                transaction.TransactionType,

            Status = transaction.Status,

            Amount = transaction.Amount,

            Currency = transaction.Currency,

            Description = transaction.Description,

            SourceAccountId =
                transaction.SourceAccountId,

            DestinationAccountId =
                transaction.DestinationAccountId,

            TransferRequestId =
                transaction.TransferRequestId,

            InitiatedByUserId =
                transaction.InitiatedByUserId,

            CompletedByUserId =
                transaction.CompletedByUserId,

            CreatedAtUtc =
                transaction.CreatedAtUtc,

            CompletedAtUtc =
                transaction.CompletedAtUtc,

            LedgerEntries =
                transaction.LedgerEntries
                    .OrderBy(entry =>
                        entry.EntryType)
                    .Select(entry =>
                        new TransactionLedgerEntryDto
                        {
                            Id = entry.Id,

                            AccountId =
                                entry.AccountId,

                            AccountName =
                                entry.Account.Name,

                            AccountNumber =
                                entry.Account
                                    .AccountNumber,

                            EntryType =
                                entry.EntryType,

                            Amount =
                                entry.Amount,

                            Description =
                                entry.Description,

                            CreatedAtUtc =
                                entry.CreatedAt
                        })
                    .ToList()
        };
    }

    private static TreasuryTransactionSummaryDto
        MapSummary(
            TreasuryTransaction transaction)
    {
        return new TreasuryTransactionSummaryDto
        {
            Id = transaction.Id,

            Reference = transaction.Reference,

            TransactionType =
                transaction.TransactionType,

            Status = transaction.Status,

            Amount = transaction.Amount,

            Currency = transaction.Currency,

            Description = transaction.Description,

            SourceAccountId =
                transaction.SourceAccountId,

            DestinationAccountId =
                transaction.DestinationAccountId,

            CreatedAtUtc =
                transaction.CreatedAtUtc,

            CompletedAtUtc =
                transaction.CompletedAtUtc
        };
    }

    private static void ValidateQuery(
        TransactionQueryDto query)
    {
        if (query.Page < 1)
        {
            throw new ArgumentException(
                "Page must be greater than zero.");
        }

        if (query.PageSize < 1 ||
            query.PageSize > MaximumPageSize)
        {
            throw new ArgumentException(
                $"Page size must be between 1 " +
                $"and {MaximumPageSize}.");
        }
    }

    private static DateTime?
        NormalizeDateTime(DateTime? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value.Kind switch
        {
            DateTimeKind.Utc =>
                value.Value,

            DateTimeKind.Local =>
                value.Value.ToUniversalTime(),

            _ =>
                DateTime.SpecifyKind(
                    value.Value,
                    DateTimeKind.Utc)
        };
    }
}
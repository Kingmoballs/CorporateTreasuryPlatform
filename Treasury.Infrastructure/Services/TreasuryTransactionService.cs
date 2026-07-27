using System.Text;
using Treasury.Application.Common;
using Treasury.Application.DTOs.Exports;
using Treasury.Application.DTOs.Transactions;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Services;

public class TreasuryTransactionService
    : ITreasuryTransactionService
{
    private const int MaximumPageSize = 100;
    private const int MaximumExportRows = 10000;
    private const int DefaultActivityDays = 30;
    private const int MaximumActivityDays = 366;

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
        var filter =
            OrganizationDimensionFilter.Create(
                query.LegalEntityId,
                query.BusinessUnitId);

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
            LegalEntityId =
                filter.LegalEntityId,

            BusinessUnitId =
                filter.BusinessUnitId,

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
            
            PaymentRequestId =
                transaction.PaymentRequestId,
            
            ReversesTransactionId =
                transaction.ReversesTransactionId,
            
            ReversalRequestId =
                transaction.ReversalRequestId,
            
            ReversesTransactionReference =
                transaction.ReversesTransaction?.Reference,

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

    public async Task<TreasuryActivitySummaryDto>
        GetActivitySummary(
            TreasuryActivitySummaryQueryDto query)
    {
        var filter =
            OrganizationDimensionFilter.Create(
                query.LegalEntityId,
                query.BusinessUnitId);

        var reportToUtc =
            NormalizeDateTime(query.ToUtc)
            ?? DateTime.UtcNow;

        var reportFromUtc =
            NormalizeDateTime(query.FromUtc)
            ?? reportToUtc.AddDays(
                -DefaultActivityDays);

        if (reportFromUtc >= reportToUtc)
        {
            throw new ArgumentException(
                "The start date must be earlier " +
                "than the end date.");
        }

        if ((reportToUtc - reportFromUtc).TotalDays >
            MaximumActivityDays)
        {
            throw new ArgumentException(
                $"The activity summary period cannot exceed " +
                $"{MaximumActivityDays} days.");
        }

        query.FromUtc = reportFromUtc;
        query.ToUtc = reportToUtc;

        var transactions =
            await _transactionRepository
                .GetForActivitySummary(query);

        var completedTransactions =
            transactions
                .Where(transaction =>
                    transaction.Status ==
                        TransactionStatuses.Completed)
                .ToList();

        var byCurrency =
            completedTransactions
                .GroupBy(transaction =>
                    NormalizeCurrency(
                        transaction.Currency),
                    StringComparer.OrdinalIgnoreCase)
                .OrderBy(group =>
                    group.Key,
                    StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                    BuildCurrencyActivitySummary(
                        group.Key,
                        group.ToList()))
                .ToList();

        return new TreasuryActivitySummaryDto
        {
            GeneratedAtUtc =
                DateTime.UtcNow,

            ActivityFromUtc =
                reportFromUtc,

            ActivityToUtc =
                reportToUtc,

            LegalEntityId =
                filter.LegalEntityId,

            BusinessUnitId =
                filter.BusinessUnitId,

            TotalTransactionCount =
                transactions.Count,

            CompletedTransactionCount =
                completedTransactions.Count,

            CurrencyCount =
                byCurrency.Count,

            ByCurrency =
                byCurrency
        };
    }

    public async Task<CsvExportDto> ExportTransactionsCsv(
        TransactionQueryDto query)
    {
        var filter =
            OrganizationDimensionFilter.Create(
                query.LegalEntityId,
                query.BusinessUnitId);

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

        var transactions =
            await _transactionRepository
                .GetForExport(
                    query,
                    MaximumExportRows);

        var csv =
            new StringBuilder();

        /*
        * This report is intentionally flat so it opens
        * cleanly in Excel and can be shared with Finance,
        * Audit, or Operations.
        */
        csv.AppendLine(
            "LegalEntityId,BusinessUnitId,Id,Reference,TransactionType,Status,Amount,Currency,Description,SourceAccountId,DestinationAccountId,TransferRequestId,PaymentRequestId,ReversalRequestId,ReversesTransactionId,InitiatedByUserId,CompletedByUserId,Category,CounterpartyName,ExternalReference,CreatedAtUtc,CompletedAtUtc");

        foreach (var transaction in transactions)
        {
            csv.AppendLine(string.Join(
                ",",
                CsvExportHelper.Escape(
                    filter.LegalEntityId),
                CsvExportHelper.Escape(
                    filter.BusinessUnitId),
                CsvExportHelper.Escape(transaction.Id),
                CsvExportHelper.Escape(transaction.Reference),
                CsvExportHelper.Escape(transaction.TransactionType),
                CsvExportHelper.Escape(transaction.Status),
                CsvExportHelper.Escape(transaction.Amount),
                CsvExportHelper.Escape(transaction.Currency),
                CsvExportHelper.Escape(transaction.Description),
                CsvExportHelper.Escape(transaction.SourceAccountId),
                CsvExportHelper.Escape(transaction.DestinationAccountId),
                CsvExportHelper.Escape(transaction.TransferRequestId),
                CsvExportHelper.Escape(transaction.PaymentRequestId),
                CsvExportHelper.Escape(transaction.ReversalRequestId),
                CsvExportHelper.Escape(transaction.ReversesTransactionId),
                CsvExportHelper.Escape(transaction.InitiatedByUserId),
                CsvExportHelper.Escape(transaction.CompletedByUserId),
                CsvExportHelper.Escape(transaction.Category),
                CsvExportHelper.Escape(transaction.CounterpartyName),
                CsvExportHelper.Escape(transaction.ExternalReference),
                CsvExportHelper.Escape(transaction.CreatedAtUtc),
                CsvExportHelper.Escape(transaction.CompletedAtUtc)));
        }

        var timestamp =
            DateTime.UtcNow.ToString("yyyyMMddHHmmss");

        return new CsvExportDto
        {
            FileName =
                $"treasury-transactions-{timestamp}.csv",

            ContentType =
                "text/csv",

            Content =
                CsvExportHelper.ToUtf8Bytes(
                    csv.ToString())
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

    private static CurrencyTreasuryActivitySummaryDto
        BuildCurrencyActivitySummary(
            string currency,
            IReadOnlyList<TreasuryTransaction> transactions)
    {
        var cashReceipts =
            transactions
                .Where(transaction =>
                    transaction.TransactionType ==
                        TransactionTypes.CashReceipt)
                .ToList();

        var cashPayments =
            transactions
                .Where(transaction =>
                    transaction.TransactionType ==
                        TransactionTypes.CashPayment)
                .ToList();

        var reversals =
            transactions
                .Where(transaction =>
                    transaction.TransactionType ==
                        TransactionTypes.Reversal)
                .ToList();

        var receiptReversals =
            reversals
                .Where(transaction =>
                    transaction.ReversesTransaction?
                        .TransactionType ==
                            TransactionTypes.CashReceipt)
                .ToList();

        var paymentReversals =
            reversals
                .Where(transaction =>
                    transaction.ReversesTransaction?
                        .TransactionType ==
                            TransactionTypes.CashPayment)
                .ToList();

        var internalTransfers =
            transactions
                .Where(transaction =>
                    transaction.TransactionType ==
                        TransactionTypes.InternalTransfer)
                .ToList();

        var openingBalances =
            transactions
                .Where(transaction =>
                    transaction.TransactionType ==
                        TransactionTypes.OpeningBalance)
                .ToList();

        /*
        * Cash receipt = inflow.
        * Cash payment = outflow.
        * Reversal of payment = inflow.
        * Reversal of receipt = outflow.
        */
        var totalInflow =
            cashReceipts.Sum(transaction =>
                transaction.Amount)
            +
            paymentReversals.Sum(transaction =>
                transaction.Amount);

        var totalOutflow =
            cashPayments.Sum(transaction =>
                transaction.Amount)
            +
            receiptReversals.Sum(transaction =>
                transaction.Amount);

        return new CurrencyTreasuryActivitySummaryDto
        {
            Currency =
                currency,

            TransactionCount =
                transactions.Count,

            CashReceiptCount =
                cashReceipts.Count,

            CashReceiptAmount =
                cashReceipts.Sum(transaction =>
                    transaction.Amount),

            CashPaymentCount =
                cashPayments.Count,

            CashPaymentAmount =
                cashPayments.Sum(transaction =>
                    transaction.Amount),

            ReversalCount =
                reversals.Count,

            ReversalAmount =
                reversals.Sum(transaction =>
                    transaction.Amount),

            InternalTransferCount =
                internalTransfers.Count,

            InternalTransferVolume =
                internalTransfers.Sum(transaction =>
                    transaction.Amount),

            OpeningBalanceCount =
                openingBalances.Count,

            OpeningBalanceAmount =
                openingBalances.Sum(transaction =>
                    transaction.Amount),

            TotalInflowAmount =
                totalInflow,

            TotalOutflowAmount =
                totalOutflow,

            NetCashMovement =
                totalInflow - totalOutflow
        };
    }

    private static string NormalizeCurrency(
        string currency)
    {
        return string.IsNullOrWhiteSpace(currency)
            ? "UNKNOWN"
            : currency.Trim().ToUpperInvariant();
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

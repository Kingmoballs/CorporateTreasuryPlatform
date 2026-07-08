using Microsoft.EntityFrameworkCore;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;
using Treasury.Application.DTOs.Transactions;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Repositories;

public class TreasuryTransactionRepository
    : ITreasuryTransactionRepository
{
    private readonly TreasuryDbContext _context;

    public TreasuryTransactionRepository(
        TreasuryDbContext context)
    {
        _context = context;
    }

    public async Task Add(
        TreasuryTransaction transaction)
    {
        await _context.TreasuryTransactions
            .AddAsync(transaction);
    }

    public async Task<TreasuryTransaction?>
        GetByReference(string reference)
    {
        return await _context
            .TreasuryTransactions
            .AsNoTracking()
            .Include(transaction =>
                transaction.LedgerEntries)
            .ThenInclude(entry =>
                entry.Account)
            .Include(transaction =>
                transaction.ReversesTransaction)
            .FirstOrDefaultAsync(transaction =>
                transaction.Reference ==
                    reference);
            
    }

    public async Task<(
        IReadOnlyList<TreasuryTransaction> Items,
        int TotalCount)>
        Search(TransactionQueryDto query)
    {
        var transactions =
            _context.TreasuryTransactions
                .AsNoTracking()
                .AsQueryable();

        if (!string.IsNullOrWhiteSpace(
            query.Currency))
        {
            var currency =
                query.Currency
                    .Trim()
                    .ToUpperInvariant();

            transactions =
                transactions.Where(transaction =>
                    transaction.Currency ==
                        currency);
        }

        if (!string.IsNullOrWhiteSpace(
            query.Status))
        {
            var status = query.Status.Trim();

            transactions =
                transactions.Where(transaction =>
                    transaction.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(
            query.TransactionType))
        {
            var transactionType =
                query.TransactionType.Trim();

            transactions =
                transactions.Where(transaction =>
                    transaction.TransactionType ==
                        transactionType);
        }

        if (query.FromUtc.HasValue)
        {
            transactions =
                transactions.Where(transaction =>
                    transaction.CreatedAtUtc >=
                        query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            // The reporting end is exclusive.
            transactions =
                transactions.Where(transaction =>
                    transaction.CreatedAtUtc <
                        query.ToUtc.Value);
        }

        var totalCount =
            await transactions.CountAsync();

        var items =
            await transactions
                .OrderByDescending(transaction =>
                    transaction.CreatedAtUtc)
                .Skip(
                    (query.Page - 1) *
                    query.PageSize)
                .Take(query.PageSize)
                .ToListAsync();

        return (items, totalCount);
    }

    public async Task<TreasuryTransaction?>
        GetByIdempotencyKey(
            string idempotencyKey)
    {
        return await _context
            .TreasuryTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(transaction =>
                transaction.IdempotencyKey ==
                    idempotencyKey);
    }
    public async Task<TreasuryTransaction?>
        GetById(Guid id)
    {
        return await _context
            .TreasuryTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(transaction =>
                transaction.Id == id);
    }

    public async Task<TreasuryTransaction?>
        GetByReversedTransactionId(
            Guid originalTransactionId)
    {
        return await _context
            .TreasuryTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(transaction =>
                transaction.ReversesTransactionId ==
                    originalTransactionId);
    }

    public async Task<List<TreasuryTransaction>>
        FindPotentialReconciliationMatches(
            Guid accountId,
            decimal signedAmount,
            string currency,
            DateTime transactionDateUtc,
            int dateToleranceDays)
    {
        if (signedAmount == 0)
        {
            return new List<TreasuryTransaction>();
        }

        var absoluteAmount =
            Math.Abs(signedAmount);

        var normalizedCurrency =
            currency.Trim().ToUpperInvariant();

        var fromUtc =
            transactionDateUtc.Date
                .AddDays(-dateToleranceDays);

        var toUtc =
            transactionDateUtc.Date
                .AddDays(dateToleranceDays + 1);

        var alreadyMatchedTransactionIds =
            _context.BankStatementLines
                .Where(line =>
                    line.MatchedTreasuryTransactionId != null)
                .Select(line =>
                    line.MatchedTreasuryTransactionId!.Value);

        var query =
            _context.TreasuryTransactions
                .AsNoTracking()
                .Where(transaction =>
                    transaction.Status ==
                        TransactionStatuses.Completed &&
                    transaction.Currency ==
                        normalizedCurrency &&
                    transaction.Amount ==
                        absoluteAmount &&
                    !alreadyMatchedTransactionIds
                        .Contains(transaction.Id))
                .Where(transaction =>
                    (transaction.CompletedAtUtc ??
                        transaction.CreatedAtUtc) >= fromUtc &&
                    (transaction.CompletedAtUtc ??
                        transaction.CreatedAtUtc) < toUtc);

        /*
        * Positive bank amount means money entered this account.
        * Negative bank amount means money left this account.
        */
        if (signedAmount > 0)
        {
            query =
                query.Where(transaction =>
                    transaction.DestinationAccountId ==
                    accountId);
        }
        else
        {
            query =
                query.Where(transaction =>
                    transaction.SourceAccountId ==
                    accountId);
        }

        return await query
            .OrderBy(transaction =>
                transaction.CompletedAtUtc ??
                transaction.CreatedAtUtc)
            .Take(5)
            .ToListAsync();
    }
}
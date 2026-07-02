using Microsoft.EntityFrameworkCore;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;
using Treasury.Application.DTOs.Transactions;

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
}
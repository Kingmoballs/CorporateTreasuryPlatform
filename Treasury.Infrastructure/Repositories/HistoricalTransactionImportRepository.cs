using Microsoft.EntityFrameworkCore;
using Treasury.Application.DTOs.HistoricalImports;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Infrastructure.Persistence;
using Treasury.Shared.Constants;
using Microsoft.EntityFrameworkCore.Storage;

namespace Treasury.Infrastructure.Repositories;

public class HistoricalTransactionImportRepository
    : IHistoricalTransactionImportRepository
{
    private readonly TreasuryDbContext _context;

    private IDbContextTransaction? _transaction;

    public HistoricalTransactionImportRepository(
        TreasuryDbContext context)
    {
        _context = context;
    }

    public Task<HistoricalTransactionImportBatch?>
        GetByImportKey(Guid importKey)
    {
        return BatchQuery()
            .FirstOrDefaultAsync(batch =>
                batch.ImportKey == importKey);
    }

    public Task<HistoricalTransactionImportBatch?>
        GetByFileHash(
            string mode,
            string fileHash)
    {
        return BatchQuery()
            .FirstOrDefaultAsync(batch =>
                batch.Mode == mode &&
                batch.FileHash == fileHash);
    }

    public Task<HistoricalTransactionImportBatch?>
        GetBatch(Guid batchId)
    {
        return BatchQuery()
            .FirstOrDefaultAsync(batch =>
                batch.Id == batchId);
    }

    public Task<HistoricalTransactionImportBatch?>
        GetBatchForUpdate(Guid batchId)
    {
        return _context
            .HistoricalTransactionImportBatches
            .Include(batch => batch.Rows)
            .Include(batch => batch.Decisions)
                .ThenInclude(decision =>
                    decision.ApproverUser)
            .FirstOrDefaultAsync(batch =>
                batch.Id == batchId);
    }

    public async Task<IReadOnlyList<
        HistoricalTransactionImportDecision>>
        GetDecisions(Guid batchId)
    {
        return await _context
            .HistoricalTransactionImportDecisions
            .AsNoTracking()
            .Include(decision =>
                decision.ApproverUser)
            .Where(decision =>
                decision.BatchId == batchId)
            .OrderBy(decision =>
                decision.CreatedAtUtc)
            .ToListAsync();
    }

    public Task<bool> HasDecision(
        Guid batchId,
        Guid approverUserId)
    {
        return _context
            .HistoricalTransactionImportDecisions
            .AnyAsync(decision =>
                decision.BatchId == batchId &&
                decision.ApproverUserId ==
                    approverUserId);
    }

    public async Task<(IReadOnlyList<
        HistoricalTransactionImportRow> Items,
        int TotalCount)> GetRows(
            Guid batchId,
            HistoricalImportRowsQueryDto query)
    {
        var rows =
            _context.HistoricalTransactionImportRows
                .AsNoTracking()
                .Where(row =>
                    row.BatchId == batchId);

        if (query.IsValid.HasValue)
        {
            rows = rows.Where(row =>
                row.IsValid == query.IsValid.Value);
        }

        var totalCount = await rows.CountAsync();

        var items = await rows
            .OrderBy(row => row.RowNumber)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<IReadOnlyDictionary<
        string,
        Account>> GetAccountsByNumbers(
            IReadOnlyCollection<string>
                accountNumbers)
    {
        var normalizedNumbers =
            accountNumbers
                .Where(number =>
                    !string.IsNullOrWhiteSpace(number))
                .Select(number =>
                    number.Trim().ToUpperInvariant())
                .Distinct()
                .ToArray();

        var accounts =
            await _context.Accounts
                .AsNoTracking()
                .Include(account =>
                    account.LegalEntity)
                .Include(account =>
                    account.BusinessUnit)
                .Where(account =>
                    normalizedNumbers.Contains(
                        account.AccountNumber
                            .ToUpper()))
                .ToListAsync();

        return accounts.ToDictionary(
            account =>
                account.AccountNumber
                    .ToUpperInvariant(),
            StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlySet<string>>
        GetFingerprintsInValidatedBatches(
            string mode,
            IReadOnlyCollection<string>
                fingerprints,
            Guid? excludedBatchId = null)
    {
        if (fingerprints.Count == 0)
        {
            return new HashSet<string>(
                StringComparer.Ordinal);
        }

        var matches =
            await _context
                .HistoricalTransactionImportRows
                .AsNoTracking()
                .Where(row =>
                    row.IsValid &&
                    fingerprints.Contains(
                        row.Fingerprint) &&
                    row.Batch.Mode == mode &&
                    row.Batch.Status !=
                        HistoricalImportStatuses
                            .ValidationFailed &&
                    row.Batch.Status !=
                        HistoricalImportStatuses
                            .Rejected &&
                    (!excludedBatchId.HasValue ||
                     row.BatchId !=
                        excludedBatchId.Value))
                .Select(row => row.Fingerprint)
                .Distinct()
                .ToListAsync();

        return matches.ToHashSet(
            StringComparer.Ordinal);
    }

    public async Task<IReadOnlySet<Guid>>
        GetAccountIdsWithFinancialActivity(
            IReadOnlyCollection<Guid> accountIds)
    {
        if (accountIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var ledgerAccountIds =
            await _context.LedgerEntries
                .AsNoTracking()
                .Where(entry =>
                    accountIds.Contains(
                        entry.AccountId))
                .Select(entry => entry.AccountId)
                .Distinct()
                .ToListAsync();

        var sourceAccountIds =
            await _context.TreasuryTransactions
                .AsNoTracking()
                .Where(transaction =>
                    transaction.SourceAccountId
                        .HasValue &&
                    accountIds.Contains(
                        transaction.SourceAccountId
                            .Value))
                .Select(transaction =>
                    transaction.SourceAccountId!.Value)
                .Distinct()
                .ToListAsync();

        var destinationAccountIds =
            await _context.TreasuryTransactions
                .AsNoTracking()
                .Where(transaction =>
                    transaction.DestinationAccountId
                        .HasValue &&
                    accountIds.Contains(
                        transaction
                            .DestinationAccountId
                            .Value))
                .Select(transaction =>
                    transaction
                        .DestinationAccountId!.Value)
                .Distinct()
                .ToListAsync();

        return ledgerAccountIds
            .Concat(sourceAccountIds)
            .Concat(destinationAccountIds)
            .ToHashSet();
    }

    public async Task<IReadOnlyDictionary<Guid, Account>>
        GetAccountsForUpdate(
            IReadOnlyCollection<Guid> accountIds)
    {
        var accounts =
            await _context.Accounts
                .Include(account =>
                    account.LegalEntity)
                .Include(account =>
                    account.BusinessUnit)
                .Where(account =>
                    accountIds.Contains(account.Id))
                .ToListAsync();

        return accounts.ToDictionary(
            account => account.Id);
    }

    public async Task AddDecision(
        HistoricalTransactionImportDecision
            decision)
    {
        await _context
            .HistoricalTransactionImportDecisions
            .AddAsync(decision);
    }

    public Task AddHistoricalRecords(
        IReadOnlyCollection<
            HistoricalTransactionRecord> records)
    {
        return _context.HistoricalTransactionRecords
            .AddRangeAsync(records);
    }

    public Task AddTreasuryTransactions(
        IReadOnlyCollection<
            TreasuryTransaction> transactions)
    {
        return _context.TreasuryTransactions
            .AddRangeAsync(transactions);
    }

    public Task AddLedgerEntries(
        IReadOnlyCollection<LedgerEntry> entries)
    {
        return _context.LedgerEntries
            .AddRangeAsync(entries);
    }

    public async Task<(IReadOnlyList<
        HistoricalTransactionRecord> Items,
        int TotalCount)> GetCommittedRecords(
            HistoricalTransactionRecordQueryDto query)
    {
        var records =
            _context.HistoricalTransactionRecords
                .AsNoTracking()
                .Include(record => record.Account)
                .AsQueryable();

        if (query.AccountId.HasValue)
        {
            records = records.Where(record =>
                record.AccountId ==
                    query.AccountId.Value);
        }

        if (query.LegalEntityId.HasValue)
        {
            records = records.Where(record =>
                record.LegalEntityId ==
                    query.LegalEntityId.Value);
        }

        if (query.BusinessUnitId.HasValue)
        {
            records = records.Where(record =>
                record.BusinessUnitId ==
                    query.BusinessUnitId.Value);
        }

        if (query.FromUtc.HasValue)
        {
            records = records.Where(record =>
                record.TransactionDateUtc >=
                    query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            records = records.Where(record =>
                record.TransactionDateUtc <
                    query.ToUtc.Value);
        }

        var totalCount =
            await records.CountAsync();

        var items =
            await records
                .OrderByDescending(record =>
                    record.TransactionDateUtc)
                .ThenBy(record =>
                    record.ExternalReference)
                .Skip(
                    (query.Page - 1) *
                    query.PageSize)
                .Take(query.PageSize)
                .ToListAsync();

        return (items, totalCount);
    }

    public void SetOriginalConcurrencyToken(
        HistoricalTransactionImportBatch batch,
        Guid concurrencyToken)
    {
        _context.Entry(batch)
            .Property(item =>
                item.ConcurrencyToken)
            .OriginalValue = concurrencyToken;
    }

    public async Task Add(
        HistoricalTransactionImportBatch batch)
    {
        await _context
            .HistoricalTransactionImportBatches
            .AddAsync(batch);
    }

    public Task SaveChanges()
    {
        return _context.SaveChangesAsync();
    }

    public async Task BeginTransaction()
    {
        _transaction =
            await _context.Database
                .BeginTransactionAsync();
    }

    public async Task CommitTransaction()
    {
        if (_transaction is null)
        {
            return;
        }

        try
        {
            await _transaction.CommitAsync();
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransaction()
    {
        if (_transaction is null)
        {
            return;
        }

        try
        {
            await _transaction.RollbackAsync();
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    private IQueryable<
        HistoricalTransactionImportBatch>
        BatchQuery()
    {
        return _context
            .HistoricalTransactionImportBatches
            .AsNoTracking();
    }
}

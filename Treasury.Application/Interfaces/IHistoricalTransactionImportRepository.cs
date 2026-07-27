using Treasury.Application.DTOs.HistoricalImports;
using Treasury.Domain.Entities;

namespace Treasury.Application.Interfaces;

public interface IHistoricalTransactionImportRepository
{
    Task<HistoricalTransactionImportBatch?>
        GetByImportKey(Guid importKey);

    Task<HistoricalTransactionImportBatch?>
        GetByFileHash(string mode, string fileHash);

    Task<HistoricalTransactionImportBatch?>
        GetBatch(Guid batchId);

    Task<HistoricalTransactionImportBatch?>
        GetBatchForUpdate(Guid batchId);

    Task<IReadOnlyList<
        HistoricalTransactionImportDecision>>
        GetDecisions(Guid batchId);

    Task<bool> HasDecision(
        Guid batchId,
        Guid approverUserId);

    Task<(IReadOnlyList<
        HistoricalTransactionImportRow> Items,
        int TotalCount)> GetRows(
            Guid batchId,
            HistoricalImportRowsQueryDto query);

    Task<IReadOnlyDictionary<string, Account>>
        GetAccountsByNumbers(
            IReadOnlyCollection<string>
                accountNumbers);

    Task<IReadOnlySet<string>>
        GetFingerprintsInValidatedBatches(
            string mode,
            IReadOnlyCollection<string>
                fingerprints,
            Guid? excludedBatchId = null);

    Task<IReadOnlySet<Guid>>
        GetAccountIdsWithFinancialActivity(
            IReadOnlyCollection<Guid> accountIds);

    Task<IReadOnlyDictionary<Guid, Account>>
        GetAccountsForUpdate(
            IReadOnlyCollection<Guid> accountIds);

    Task AddDecision(
        HistoricalTransactionImportDecision
            decision);

    Task AddHistoricalRecords(
        IReadOnlyCollection<
            HistoricalTransactionRecord> records);

    Task AddTreasuryTransactions(
        IReadOnlyCollection<
            TreasuryTransaction> transactions);

    Task AddLedgerEntries(
        IReadOnlyCollection<LedgerEntry> entries);

    Task<(IReadOnlyList<
        HistoricalTransactionRecord> Items,
        int TotalCount)> GetCommittedRecords(
            HistoricalTransactionRecordQueryDto query);

    void SetOriginalConcurrencyToken(
        HistoricalTransactionImportBatch batch,
        Guid concurrencyToken);

    Task Add(
        HistoricalTransactionImportBatch batch);

    Task SaveChanges();

    Task BeginTransaction();

    Task CommitTransaction();

    Task RollbackTransaction();
}

using Treasury.Application.DTOs.Transactions;
using Treasury.Domain.Entities;

namespace Treasury.Application.Interfaces;

public interface ITreasuryTransactionRepository
{
    Task Add(
        TreasuryTransaction transaction);

    Task<TreasuryTransaction?>
        GetByReference(string reference);

    Task<(
        IReadOnlyList<TreasuryTransaction> Items,
        int TotalCount)>
        Search(TransactionQueryDto query);

    Task<IReadOnlyList<TreasuryTransaction>> GetForExport(
        TransactionQueryDto query,
        int maxRows);

    Task<IReadOnlyList<TreasuryTransaction>> GetForActivitySummary(
        TreasuryActivitySummaryQueryDto query);
        
    Task<TreasuryTransaction?>
        GetByIdempotencyKey(
            string idempotencyKey);
    
    Task<TreasuryTransaction?> GetById(Guid id);

    Task<TreasuryTransaction?>
        GetByReversedTransactionId(
            Guid originalTransactionId);
    
    Task<List<TreasuryTransaction>>
        FindPotentialReconciliationMatches(
            Guid accountId,
            decimal signedAmount,
            string currency,
            DateTime transactionDateUtc,
            int dateToleranceDays);

    Task<List<TreasuryTransaction>>
        GetUnmatchedCompletedTransactionsForReconciliation(
            Guid accountId,
            string currency,
            DateTime? fromUtc,
            DateTime? toUtc);
    
    Task<IReadOnlyList<TreasuryTransaction>>
        GetCompletedCashFlowTransactionsForVariance(
            Guid? accountId,
            string currency,
            DateTime fromUtc,
            DateTime toUtc);
}
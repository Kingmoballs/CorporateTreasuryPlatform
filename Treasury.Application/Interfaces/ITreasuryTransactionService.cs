using Treasury.Application.DTOs.Transactions;
using Treasury.Application.DTOs.Exports;

namespace Treasury.Application.Interfaces;

public interface ITreasuryTransactionService
{
    Task<PagedTreasuryTransactionsDto>
        SearchTransactions(
            TransactionQueryDto query);

    Task<TreasuryTransactionDetailDto>
        GetByReference(string reference);
    
    Task<CsvExportDto> ExportTransactionsCsv(
        TransactionQueryDto query);
    
    Task<TreasuryActivitySummaryDto> GetActivitySummary(
        TreasuryActivitySummaryQueryDto query);
}
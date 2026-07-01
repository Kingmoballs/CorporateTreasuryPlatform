using Treasury.Application.DTOs.Transactions;

namespace Treasury.Application.Interfaces;

public interface ITreasuryTransactionService
{
    Task<PagedTreasuryTransactionsDto>
        SearchTransactions(
            TransactionQueryDto query);

    Task<TreasuryTransactionDetailDto>
        GetByReference(string reference);
}